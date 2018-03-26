using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;

namespace ToSPatchAutoExtractor {
  /// <summary>
  /// ・-・
  /// </summary>
  class Program {
    /// <summary>
    /// プログラムスタート！
    /// ToSPatchAutoExtractor.exe -u /path/to/ipf_unpack.exe -t /path/to/TreeofSaviorJP -i animation.ifp bg.ifp ...
    /// </summary>
    /// <param name="args">コマンドライン引数</param>
    static void Main(string[] args) {
      // 割り込みイベント登録
      using (var cancellationSource = new CancellationTokenSource()) {
        Console.CancelKeyPress += (sender, e) => {
          Console.WriteLine("中断検知...");
          cancellationSource.Cancel();
        };

        try {
          Console.WriteLine("--- 開始 ---");
          // 引数解析
          var myArgs = new Argument(args);
          Console.WriteLine("引数解析...");
          Console.WriteLine(myArgs);
          var files = myArgs.CreateFileList();
          Console.WriteLine("トータル処理対象...{0}件", files.Count);

          // 計測
          var watch = new Stopwatch();
          watch.Start();

          // 処理開始(Nothing -> Copy -> Decrypt)
          CreateCopyAndDecryptTasks(files, cancellationSource.Token);

          watch.Stop();
          Console.WriteLine("コピー -> 復号済...{0}", watch.Elapsed);

          // 処理開始(Extract)
          ExtractAndDeleteTasks(files, cancellationSource.Token);

          watch.Stop();
          Console.WriteLine("展開済...{0}", watch.Elapsed);

          // すべてEndになっているか確認
          if (!files.All(item => item.Status is End)) {
            Console.WriteLine("処理が終わっていない...なにかおかしい...");
            files.ToList().ForEach(item => {
              Console.WriteLine("{0} => {1}", item.FilePath, item.Status);
            });
          }

          watch.Reset();

        } catch (Exception ex) {
          Console.WriteLine("例外発生!!!\n{0}", ex);
          cancellationSource.Cancel();
        } finally {
          Console.WriteLine("--- 終了 ---");
          // コンソールを消さないためのキー入力待ち
          Console.ReadLine();
        }
      }
    }

    /// <summary>
    /// コピー->復号処理実行
    /// </summary>
    /// <param name="files">ファイル一覧</param>
    /// <param name="pToken">キャンセルトークン</param>
    protected static void CreateCopyAndDecryptTasks(IList<IPFFile> files, CancellationToken pToken) {
      // 順不同で３回やればいい
      var tasks = new List<Task>();
      var options = new ParallelOptions() {
        CancellationToken = pToken,
        MaxDegreeOfParallelism = Environment.ProcessorCount,
      };
      Console.WriteLine("コピー/復号処理対象...{0}件", files.Count);
      Parallel.ForEach(files, options, item => {
        tasks.Add(Task.Factory.StartNew(() => {
          for (var i = 0; i < 3; i++) {
            item.Status.ToDo().Next();
            pToken.ThrowIfCancellationRequested();
          }
        }, pToken));
      });
      Task.WaitAll(tasks.ToArray(), pToken);
    }

    /// <summary>
    /// 展開削除処理実行
    /// </summary>
    /// <param name="files">ファイル一覧</param>
    /// <param name="pToken">キャンセルトークン</param>
    /// <param name="action">ファイル処理後のアクション</param>
    protected static void ExtractAndDeleteTasks(IList<IPFFile> files, CancellationToken pToken) {
      // 順番大事!!!
      Console.WriteLine("展開処理対象...{0}件", files.Count);
      Task.Run(() => {
        files.ToList().ForEach(item => {
          // 展開
          item.Status.ToDo().Next();
          pToken.ThrowIfCancellationRequested();
          // 削除
          item.Status.ToDo().Next();
          pToken.ThrowIfCancellationRequested();
        });
      }).Wait(pToken);
    }

  }

  /// <summary>
  /// アプリケーションのコマンドライン引数を管理しつつ色々する（ぇ
  /// </summary>
  class Argument {
    /// <summary>
    /// IPF操作ツールのパス
    /// </summary>
    public string ToolPath { get; protected set; }

    /// <summary>
    /// ToSがインストールされているパス
    /// </summary>
    protected string _ToSPath { get; set; }
    /// <summary>
    /// ToSデータフォルダー
    /// </summary>
    protected string ToSDataPath { get { return string.Concat(_ToSPath, "/data"); } }
    /// <summary>
    /// ToSパッチフォルダー
    /// </summary>
    protected string ToSPatchPath { get { return string.Concat(_ToSPath, "/patch"); } }

    /// <summary>
    /// IPF操作ツールのパス
    /// </summary>
    public List<string> Ignores { get; protected set; }

    /// <summary>
    /// 引数解析用のCommandLineApplicationを構成->解析する
    /// </summary>
    /// <param name="pArgs">コマンドライン引数</param>
    public Argument(string[] pArgs) {
      // 初期値
      this.ToolPath = "./ipf_unpack.exe";
      this._ToSPath = @"E:\Nexon\TreeofSaviorJP";
      this.Ignores = new List<string>();
      // コマンドライン引数解析
      var app = new CommandLineApplication();
      var optToolPath = app.Option("-u", "path to ipf_unpack.exe .", CommandOptionType.SingleValue);
      var optToSPath = app.Option("-t", "path to TreeofSaviorJP .", CommandOptionType.SingleValue);
      var optIgnore = app.Option("-i", "ignore ifp files .", CommandOptionType.SingleValue);
      app.OnExecute(() => {
        if (optToolPath.HasValue()) {
          this.ToolPath = optToolPath.Value();
        }
        if (optToSPath.HasValue()) {
          this._ToSPath = optToSPath.Value();
        }
        if (optIgnore.HasValue()) {
          this.Ignores.AddRange(optIgnore.Value().Split(' '));
        }
        return 0;
      });
      app.Execute(pArgs);
    }

    /// <summary>
    /// ファイル一覧作成
    /// </summary>
    /// <returns><ファイル一覧/returns>
    public IList<IPFFile> CreateFileList() {
      List<IPFFile> files = new List<IPFFile>();
      // dataフォルダのipfを一覧化
      files.AddRange(Directory.EnumerateFiles(ToSDataPath, "*.ipf").OrderBy(item => item).Select(item => {
        return new IPFFile(item, AbstractIPFUnpack.CreateFactory(this.ToolPath));
      }));
      // patchフォルダのipfを一覧化
      files.AddRange(Directory.EnumerateFiles(ToSPatchPath, "*.ipf").OrderBy(item => item).Select(item => {
        return new IPFFile(item, AbstractIPFUnpack.CreateFactory(this.ToolPath));
      }));
      // オプション指定の無視するifpを除外
      files = files.Where(item => !this.Ignores.Contains(Path.GetFileName(item.FilePath))).ToList();
      return files;
    }

    public override string ToString() {
      var b = new StringBuilder();
      b.AppendLine("ToolPath=" + this.ToolPath);
      b.AppendLine("TosPath =" + this._ToSPath);
      b.AppendLine("Ignore  =" + this.Ignores.Count);
      foreach (var item in this.Ignores) {
        b.AppendLine("  " + item);
      }
      return b.ToString();
    }
  }
}
