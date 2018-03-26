using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToSPatchAutoExtractor {
  /// <summary>
  /// IPFUnpackingツール操作
  /// </summary>
  interface IIPFUnpack {
    /// <summary>
    /// 復号
    /// </summary>
    /// <param name="pPath">復号対象</param>
    /// <param name="pAction">復号終了処理</param>
    void Decrypt(string pPath, Action<object, EventArgs> pAction);

    /// <summary>
    /// 展開
    /// </summary>
    /// <param name="pPath">展開対象</param>
    /// <param name="pAction">展開終了処理</param>
    void Extract(string pPath, Action<object, EventArgs> pAction);
  }

  /// <summary>
  /// IPFUnpackingツール操作抽象化
  /// ツールパスとFactoryメソッドを実装する
  /// </summary>
  abstract class AbstractIPFUnpack : IIPFUnpack {
    /// <summary>
    /// ツールパス
    /// </summary>
    protected string _ToolPath { get; set; }

    /// <summary>
    /// ツールパスを受け取る
    /// </summary>
    /// <param name="pToolPath">ツールパス</param>
    public AbstractIPFUnpack(string pToolPath) {
      this._ToolPath = pToolPath;
    }

    /// <summary>
    /// 復号
    /// </summary>
    /// <param name="pPath">復号対象</param>
    /// <param name="pAction">復号終了処理</param>
    abstract public void Decrypt(string pPath, Action<object, EventArgs> pAction);
    /// <summary>
    /// 展開
    /// </summary>
    /// <param name="pPath">展開対象</param>
    /// <param name="pAction">展開終了処理</param>
    abstract public void Extract(string pPath, Action<object, EventArgs> pAction);

    /// <summary>
    /// ツールに応じた操作クラスを払い出す
    /// </summary>
    /// <param name="pToolPath">ツールパス</param>
    /// <returns>IPFUnpackingツール操作</returns>
    public static IIPFUnpack CreateFactory(string pToolPath) {
      if (pToolPath.Contains("ipf_unpack.exe")) {
        return new IPFUnpack(pToolPath);
      }
      return new MockIPFUnpack(pToolPath);
    }
  }

  /// <summary>
  /// ipf_unpack.exe用
  /// </summary>
  class IPFUnpack : AbstractIPFUnpack {

    /// <summary>
    /// ツールパスを受け取る
    /// </summary>
    /// <param name="pToolPath">ツールパス</param>
    public IPFUnpack(string pToolPath) : base(pToolPath) { }

    /// <summary>
    /// 復号
    /// </summary>
    /// <param name="pPath">復号対象</param>
    /// <param name="pAction">復号終了処理</param>
    public override void Decrypt(string pPath, Action<object, EventArgs> pAction) {
      var process = this.CreateProcess();
      process.StartInfo.FileName = this._ToolPath;
      process.StartInfo.Arguments = pPath + " decrypt";
      process.Exited += (sender, e) => { pAction(sender, e); };
      process.Start();
      Console.WriteLine(" -> {0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);
    }

    /// <summary>
    /// 展開
    /// </summary>
    /// <param name="pPath">展開対象</param>
    /// <param name="pAction">展開終了処理</param>
    public override void Extract(string pPath, Action<object, EventArgs> pAction) {
      var process = this.CreateProcess();
      process.StartInfo.FileName = this._ToolPath;
      process.StartInfo.Arguments = pPath + " extract";
      process.Exited += (sender, e) => { pAction(sender, e); };
      process.Start();
      Console.WriteLine(" -> {0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);
    }

    /// <summary>
    /// 外部プロセス呼び出し共通
    /// </summary>
    /// <returns></returns>
    protected Process CreateProcess() {
      var process = new Process();
      process.EnableRaisingEvents = true;
      process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
      return process;
    }
  }

  /// <summary>
  /// 未定義動作用
  /// </summary>
  class MockIPFUnpack : AbstractIPFUnpack {

    /// <summary>
    /// ツールパスを受け取る
    /// </summary>
    /// <param name="pToolPath">ツールパス</param>
    public MockIPFUnpack(string pToolPath) : base(pToolPath) { }

    /// <summary>
    /// 復号（しない
    /// </summary>
    /// <param name="pPath">復号対象</param>
    /// <param name="pAction">復号終了処理</param>
    public override void Decrypt(string pPath, Action<object, EventArgs> pAction) {
      Console.WriteLine("そのツール操作は未実装だ!!!");
    }

    /// <summary>
    /// 展開（しない
    /// </summary>
    /// <param name="pPath">展開対象</param>
    /// <param name="pAction">展開終了処理</param>
    public override void Extract(string pPath, Action<object, EventArgs> pAction) {
      Console.WriteLine("そのツール操作は未実装だ!!!");
    }
  }
}
