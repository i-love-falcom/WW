using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using Community.CsharpSqlite;
using Community.CsharpSqlite.SQLiteClient;

namespace WW
{
    class ClientMainJob
    {
        // 状態処理
        enum State
        {
            STATE_INIT = 0,
            STATE_SHUTDOWN,

            STATE_COUNT
        }
        private int stateID;
        private FSM fsm = null;
        private IFSMInterface[] stateArray = null;
        
        private const String appName = "WWClient";
        private String appFolder;
        private String logFolder;
        
        // ログ
        public String logFile { get; set; }
        public TraceLog traceLog = new TraceLog();
        
        // Socket関連
        private Socket socket = null;
        
        // Database関連
        public String dbFile;
        private Sqlite3.sqlite3 mainDb = null;

        public ClientMainJob()
        {
            appFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\" + appName;
            logFolder = appFolder + "\\log";
            
            logFile = appFolder + "LogFile_" + DateTime.Now.ToString("dd-MM-yyyy") + ".txt";
            dbFile = logFolder + "\\main.db";

            stateArray = new IFSMInterface[(int)State.STATE_COUNT];
            stateArray[(int)State.STATE_INIT] = new WWServerState_Init();
            stateArray[(int)State.STATE_SHUTDOWN] = new WWServerState_Shutdown();

            stateID = (int)State.STATE_INIT;
            fsm = new FSM(stateArray[stateID]);
         }

        ~ClientMainJob()
        {
            Close();
        }

        // 初期化処理
        public void Initialize(SourceLevels level)
        {
            InitLog(SourceLevels.All);
            InitDb();
        }

        // 終了処理
        public void Close()
        {
            CloseSocket();
            CloseDb();
            CloseLog();
        }

        // 更新処理
        public void Update()
        {
            try
            {
                // 可能なら受信

                // 状態処理
                fsm.ExecuteState(this);

                // 可能なら送信

                // 接続が切れていたら終了処理
                if (stateID != (int)State.STATE_SHUTDOWN && !socket.Connected)
                {
                    // ここに終了処理を入れる
                    socket.Disconnect(true);
                }
            }
            catch (Exception e)
            {
                traceLog.Append(TraceEventType.Error, e.Message);
            }

            
        }
        
        // ログファイル削除
        public void DeleteLog()
        {
            File.Delete(logFile);
        }

        // ログ初期化
        public void InitLog(SourceLevels level)
        {
            if (!Directory.Exists(logFolder))
            {
                Directory.CreateDirectory(logFolder);
            }
            traceLog.Init(logFile, "WWClient", level);
            WriteLog(TraceEventType.Verbose, "InitLog");
        }

        // Db初期化
        public void InitDb()
        {
            if (mainDb != null)
            {
                return;
            }
            mainDb = new Sqlite3.sqlite3();

            int rc = Sqlite3.sqlite3_open_v2(
                dbFile,
                out mainDb,
                Sqlite3.SQLITE_OPEN_READWRITE | Sqlite3.SQLITE_OPEN_CREATE | Sqlite3.SQLITE_OPEN_SHAREDCACHE,
                "");
            if (rc != Sqlite3.SQLITE_OK)
            {
                WriteLog(TraceEventType.Critical, "Database initialization failed.");
                Sqlite3.sqlite3_close(mainDb);
                return;
            }

            WriteLog(TraceEventType.Verbose, "InitDb");
        }

        // サーバーへ接続
        public bool ConnectServer(String host, int port)
        {
            CloseSocket();

            try
            {
                IPHostEntry ipHostEnt = Dns.GetHostEntry(host);
                IPEndPoint ip = null;
                foreach (var addr in ipHostEnt.AddressList)
                {
                    if (addr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ip = new IPEndPoint(addr, port);
                        break;
                    }
                }
                if (ip == null)
                {
                    Exception e = new Exception("network interfaces was not found.");
                    throw e;
                }

                socket = new Socket(
                    AddressFamily.InterNetwork,
                    SocketType.Stream,
                    ProtocolType.Tcp);

                socket.Connect(ip);

                WriteLog(TraceEventType.Information, "Connection server -- [" + host + "(" + ip.Address + ")" + ":" + port + "]");
            }
            catch (Exception e)
            {
                WriteLog(TraceEventType.Critical, e.Message);
                return false;
            }
            WriteLog(TraceEventType.Verbose, "ConnectServer");

            return true;
        }

        // ログを閉じる
        public void CloseLog()
        {
            traceLog.Close();
        }

        // Dbを閉じる
        public void CloseDb()
        {
            if (mainDb != null)
            {
                Sqlite3.sqlite3_close(mainDb);
                mainDb = null;
            }
            WriteLog(TraceEventType.Verbose, "CloseDb");
        }

        // ソケットを閉じる
        public void CloseSocket()
        {
            if (socket != null)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                socket = null;

                WriteLog(TraceEventType.Information, "Close Socket");
            }
            WriteLog(TraceEventType.Verbose, "CloseSocket");
        }

        // ログ出力
        public void WriteLog(TraceEventType ev, String msg)
        {
            traceLog.Append(ev, msg);
        }
    }
}
