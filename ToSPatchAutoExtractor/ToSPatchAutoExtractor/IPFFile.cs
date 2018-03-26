using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ToSPatchAutoExtractor {
  /// <summary>
  /// 処理対象のipfファイル1つに対応するモデル
  /// </summary>
  class IPFFile {
    /// <summary>
    /// 処理対象ファイルパス
    /// </summary>
    public string FilePath { get; set; }
    /// <summary>
    /// IPFファイル処理状態
    /// </summary>
    public IIPFStatus Status { get; set; }
    /// <summary>
    /// 復号/展開用ツール操作
    /// </summary>
    public IIPFUnpack Unpacker { get; set; }

    /// <summary>
    /// メンバーを初期化
    /// </summary>
    /// <param name="pFilePath"></param>
    public IPFFile(string pFilePath, IIPFUnpack pUnpacker) {
      this.FilePath = pFilePath;
      this.Status = new Start(this);
      this.Unpacker = pUnpacker;
    }
  }

  /// <summary>
  /// IPFファイル処理状態
  /// 正常遷移:        Start -> Copy -> Decrypt -> Extract -> Remove -> End
  /// あどーん除外遷移: Start -> End
  /// </summary>
  interface IIPFStatus {
    /// <summary>
    /// 現在の状態ですべき処理
    /// </summary>
    /// <returns>現在の状態</returns>
    IIPFStatus ToDo();

    /// <summary>
    /// 次の状態に遷移
    /// </summary>
    /// <returns>次の状態</returns>
    IIPFStatus Next();
  }

  /// <summary>
  /// IPFファイル処理状態のとりあえず抽象化（おそらく要らないが...
  /// </summary>
  abstract class AbstractIPFStatus : IIPFStatus {

    /// <summary>
    /// ipfファイルモデル
    /// </summary>
    protected IPFFile _ipfFile;

    /// <summary>
    /// 外部プロセス終了待機状態
    /// true : 処理中 / false : 終了
    /// </summary>
    protected bool _lock;

    /// <summary>
    /// 最終処理管理
    /// </summary>
    protected LastAction _lastAction;

    /// <summary>
    /// ipfファイルモデルを受け取る
    /// </summary>
    /// <param name="pIpfFile">ipfファイル</param>
    public AbstractIPFStatus(IPFFile pIpfFile) {
      this._ipfFile = pIpfFile;
      this._lastAction = new LastAction(pIpfFile.FilePath, this.GetType().Name);
    }

    /// <summary>
    /// 現在の状態ですべき処理
    /// </summary>
    /// <returns>現在の状態</returns>
    public abstract IIPFStatus ToDo();

    /// <summary>
    /// 次の状態に遷移
    /// </summary>
    /// <returns>次の状態</returns>
    public abstract IIPFStatus Next();

    /// <summary>
    /// 外部プロセス終了待機
    /// </summary>
    protected void Wait() {
      while (this._lock) {
        Thread.Sleep(1000);
      }
    }

    /// <summary>
    /// 外部プロセス終了ロック
    /// </summary>
    protected void Lock() {
      this._lock = true;
    }

    /// <summary>
    /// 外部プロセス終了アンロック
    /// </summary>
    protected void UnLock() {
      this._lock = false;
    }
  }

  /// <summary>
  /// IPFファイル初期状態
  /// あどーんのファイルは除外する
  /// </summary>
  class Start : AbstractIPFStatus {
    /// <summary>
    /// True: あどーん / False: あどーんでない
    /// </summary>
    private bool __addon;

    /// <summary>
    /// ipfファイルモデルを受け取る
    /// </summary>
    /// <param name="pIpfFile">ipfファイル</param>
    public Start(IPFFile pIpfFile) : base(pIpfFile) { }

    /// <summary>
    /// あどーん除外
    /// </summary>
    /// <returns>現在の状態</returns>
    public override IIPFStatus ToDo() {
      // 英数._で構成されていないファイルはあどーん
      this.__addon = !Regex.IsMatch(Path.GetFileName(_ipfFile.FilePath), "^[0-9a-z._]+$");
      Console.WriteLine("あどーんチェック中...{0} = {1}", _ipfFile.FilePath, this.__addon);
      return this;
    }

    /// <summary>
    /// あどーんならEnd、それ以外はCopy処理に遷移
    /// </summary>
    /// <returns>Copy / End</returns>
    public override IIPFStatus Next() {
      if (this.__addon) {
        this._ipfFile.Status = new End(this._ipfFile);
        return this._ipfFile.Status;
      }
      this._ipfFile.Status = new Copy(this._ipfFile);
      return this._ipfFile.Status;
    }
  }

  /// <summary>
  /// IPFファイルコピー状態
  /// ファイルをコピーして、IFPファイルモデルのファイル名を書き換える
  /// </summary>
  class Copy : AbstractIPFStatus {
    /// <summary>
    /// コピー先一時ディレクトリ
    /// </summary>
    private readonly string __tempDir = "./temp";

    /// <summary>
    /// ipfファイルモデルを受け取る
    /// </summary>
    /// <param name="pIpfFile">ipfファイル</param>
    public Copy(IPFFile pIpfFile) : base(pIpfFile) { }

    /// <summary>
    /// IPFファイルを一時ディレクトリにコピー
    /// </summary>
    /// <returns>現在の状態</returns>
    public override IIPFStatus ToDo() {
      // 一時ディレクトリ作成
      if (!Directory.Exists(__tempDir)) {
        Directory.CreateDirectory(__tempDir);
        Console.WriteLine("一時ディレクトリ作成...{0}", __tempDir);
      }
      // コピーチェック
      var destPath = Path.Combine(__tempDir, Path.GetFileName(this._ipfFile.FilePath));
      if (this._lastAction.IsActionOver()) {
        Console.WriteLine("コピー済み...{0}", destPath);
        return this;
      }
      // Copy処理
      Console.WriteLine("コピー中...{0} -> {1}", this._ipfFile.FilePath, destPath);
      File.Copy(this._ipfFile.FilePath, destPath, true);
      // 処理対象ファイルパスをコピー先にする
      this._ipfFile.FilePath = destPath;
      return this;
    }

    /// <summary>
    /// Decrypt処理へ遷移
    /// </summary>
    /// <returns>Decrypt</returns>
    public override IIPFStatus Next() {
      // 処理結果を保存
      this._lastAction.Save();
      // 遷移
      this._ipfFile.Status = new Decrypt(this._ipfFile);
      return this._ipfFile.Status;
    }
  }

  /// <summary>
  /// 復号状態
  /// 一時ファイルを復号する
  /// </summary>
  class Decrypt : AbstractIPFStatus {

    /// <summary>
    /// ipfファイルモデルを受け取る
    /// </summary>
    /// <param name="pIpfFile">ipfファイル</param>
    public Decrypt(IPFFile pIpfFile) : base(pIpfFile) { }

    /// <summary>
    /// IPFファイルを復号
    /// </summary>
    /// <returns></returns>
    public override IIPFStatus ToDo() {
      // 復号チェック
      if (this._lastAction.IsActionOver()) {
        Console.WriteLine("復号スキップ...{0}", this._ipfFile.FilePath);
        return this;
      }
      // 復号
      this.Lock();
      Console.WriteLine("復号中...{0}", _ipfFile.FilePath);
      this._ipfFile.Unpacker.Decrypt(this._ipfFile.FilePath, (sender, e) => { this.UnLock(); });
      return this;
    }

    /// <summary>
    /// Extractに遷移
    /// </summary>
    /// <returns></returns>
    public override IIPFStatus Next() {
      this.Wait();
      // 処理結果を保存
      this._lastAction.Save();
      // 遷移
      this._ipfFile.Status = new Extract(this._ipfFile);
      return this._ipfFile.Status;
    }
  }

  /// <summary>
  /// 展開状態
  /// 一時ファイルを展開する
  /// </summary>
  class Extract : AbstractIPFStatus {

    /// <summary>
    /// ipfファイルモデルを受け取る
    /// </summary>
    /// <param name="pIpfFile">ipfファイル</param>
    public Extract(IPFFile pIpfFile) : base(pIpfFile) { }

    /// <summary>
    /// IPFファイル展開
    /// </summary>
    /// <returns></returns>
    public override IIPFStatus ToDo() {
      // 展開チェック
      if (this._lastAction.IsActionOver()) {
        Console.WriteLine("展開スキップ...{0}", this._ipfFile.FilePath);
        return this;
      }
      // 展開
      this.Lock();
      Console.WriteLine("展開中...{0}", _ipfFile.FilePath);
      this._ipfFile.Unpacker.Extract(this._ipfFile.FilePath, (sender, e) => { this.UnLock(); });
      return this;
    }

    /// <summary>
    /// Removeに遷移
    /// </summary>
    /// <returns></returns>
    public override IIPFStatus Next() {
      this.Wait();
      // 処理結果を保存
      this._lastAction.Save();
      // 遷移
      this._ipfFile.Status = new Remove(this._ipfFile);
      return this._ipfFile.Status;
    }
  }

  /// <summary>
  /// IPFファイル処理削除状態
  /// </summary>
  class Remove : AbstractIPFStatus {
    /// <summary>
    /// ipfファイルモデルを受け取る
    /// </summary>
    /// <param name="pIpfFile">ipfファイル</param>
    public Remove(IPFFile pIpfFile) : base(pIpfFile) { }

    /// <summary>
    /// 一時ファイルを削除
    /// </summary>
    /// <returns></returns>
    public override IIPFStatus ToDo() {
      Console.WriteLine("削除中...{0}", _ipfFile.FilePath);
      File.Delete(this._ipfFile.FilePath);
      return this;
    }

    /// <summary>
    /// End処理に遷移
    /// </summary>
    /// <returns>Remove</returns>
    public override IIPFStatus Next() {
      this._ipfFile.Status = new End(this._ipfFile);
      return this._ipfFile.Status;
    }
  }

  /// <summary>
  /// IPFファイル処理終了状態
  /// </summary>
  class End : AbstractIPFStatus {
    /// <summary>
    /// ipfファイルモデルを受け取る
    /// </summary>
    /// <param name="pIpfFile">ipfファイル</param>
    public End(IPFFile pIpfFile) : base(pIpfFile) { }

    /// <summary>
    /// 何もしない！
    /// </summary>
    /// <returns></returns>
    public override IIPFStatus ToDo() {
      return this;
    }

    /// <summary>
    /// お疲れ様でした！
    /// </summary>
    /// <returns></returns>
    public override IIPFStatus Next() {
      return this;
    }
  }

  /// <summary>
  /// 最後の操作を記録
  /// ipfのコピー/復号/展開を記録することが目的
  /// </summary>
  class LastAction {

    private readonly string __saveDir = "./save";

    protected string _savePath;

    public LastAction(string pFile, string pStatus) {
      this._savePath = Path.Combine(this.__saveDir, string.Concat(Path.GetFileName(pFile), ".", pStatus.ToLower()));
    }

    public void Save() {
      // 一時ディレクトリ作成
      if (!Directory.Exists(__saveDir)) {
        Directory.CreateDirectory(__saveDir);
        Console.WriteLine("一時ディレクトリ作成...{0}", __saveDir);
      }
      using (var file = File.Create(this._savePath)) { }
    }

    public bool IsActionOver() {
      return File.Exists(this._savePath);
    }
  }
}
