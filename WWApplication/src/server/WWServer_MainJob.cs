using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using Community.CsharpSqlite;
using Community.CsharpSqlite.SQLiteClient;

namespace WW
{
    public class ServerMainJob
    {
        private const String appName = "WWServer";
        private String appFolder;
        private String logFolder;

        // ログ
        public String logFile { get; set; }
        private TraceLog traceLog = new TraceLog();

        // Database
        private String dbFile;
        
        private AcceptWorker acceptWorker = null;
        private Thread acceptThread = null;

        public ServerMainJob()
        {
            appFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\" + appName;
            logFolder = appFolder + "\\log";
            
            logFile = appFolder + "LogFile_" + DateTime.Now.ToString("dd-MM-yyyy") + ".txt";
            dbFile = logFolder + "\\main.db";
        }

        ~ServerMainJob()
        {
            CloseServer();
            CloseLog();
        }
        
        // ログファイル削除
        public void DeleteLog()
        {
            File.Delete(logFile);
        }

        // ログ初期化
        public void InitLog(SourceLevels level)
        {
            try
            {
                if (!Directory.Exists(logFolder))
                {
                    Directory.CreateDirectory(logFolder);
                }
                traceLog.Init(logFile, "WWServer", level);
            }
            catch (Exception e)
            {
                WriteLog(TraceEventType.Error, "InitLog" + e.Message);
            }
            WriteLog(TraceEventType.Verbose, "InitLog");
        }
        
        // サーバ初期化
        public void InitServer()
        {
            if (acceptWorker == null)
            {
                acceptWorker = new AcceptWorker(traceLog);
                acceptWorker.InitDb(dbFile);
            }
        }

        // サーバを閉じる
        public void CloseServer()
        {
            if (acceptWorker != null)
            {
                acceptWorker.CloseDb();
                acceptWorker = null;
            }
        }

        // ログを閉じる
        public void CloseLog()
        {
            traceLog.Close();
        }
        
        // 接続受付開始
        public bool StartListening(NetworkInterface nic, int port)
        {
            if (acceptWorker != null)
            {
                try
                {
                    acceptWorker.Reset();

                    if (!acceptWorker.InitSocket(nic, port))
                    {
                        return false;
                    }
                    if (acceptThread == null)
                    {
                        acceptThread = new Thread(acceptWorker.Execute, 0);
                    }
                    acceptThread.Start();

                    // 起動まで待つ
                    while (!acceptThread.IsAlive) ;
                }
                catch (Exception e)
                {
                    WriteLog(TraceEventType.Error, "StartListening: " + e.Message);
                    StopListening();
                    return false;
                }
            }
            return true;
        }

        // 接続受付終了
        public void StopListening()
        {
            try
            {
                if (acceptWorker != null)
                {
                    acceptWorker.RequestTerminate();
                }
                if (acceptThread != null)
                {
                    acceptThread.Join();
                    acceptThread = null;
                }
            }
            catch (Exception e)
            {
                WriteLog(TraceEventType.Error, "StartListening: " + e.Message);
            }
            finally
            {
                if (acceptWorker != null)
                {
                    acceptWorker.CloseSocket();
                }
            }
        }

        // ログ出力
        public void WriteLog(TraceEventType ev, String msg)
        {
            traceLog.Append(ev, msg);
        }
        
        // 接続待ち受け用ワーカークラス
        private class AcceptWorker
        {
            public int AcceptTimeOut { get; set; }
            
            // Socket関連
            private Socket listenerSocket = null;
            private Hashtable workerSocketHash = Hashtable.Synchronized(new Hashtable());
            private int workerSocketId = 0;
            private int numAcceptBacklog = 100;
            private ManualResetEvent acceptDone = new ManualResetEvent(false);
            private volatile bool stopListening = false;

            // Database関連
            private Sqlite3.sqlite3 mainDb = null;

            // ログ
            private TraceLog traceLog = null;
            
            
            public AcceptWorker(TraceLog log)
            {
                traceLog = log;
                AcceptTimeOut = 100;
            }

            // ソケットを初期化
            public bool InitSocket(NetworkInterface nic, int port)
            {
                try
                {
                    IPEndPoint ip = null;
                    {
                        bool found = false;

                        IPHostEntry ipHostEnt = Dns.GetHostEntry(Dns.GetHostName());
                        IPInterfaceProperties nicProperties = nic.GetIPProperties();
                        foreach (IPAddressInformation ipInfo in nicProperties.UnicastAddresses)
                        {
                            IPAddress nicIP = ipInfo.Address;
                            if (!IPAddress.IsLoopback(nicIP) &&
                                nicIP.AddressFamily == AddressFamily.InterNetwork
                            )
                            {
                                foreach (IPAddress addr in ipHostEnt.AddressList)
                                {
                                    if (addr.Equals(nicIP))
                                    {
                                        ip = new IPEndPoint(addr, port);
                                        found = true;
                                        break;
                                    }
                                }
                            }
                            if (found)
                            {
                                break;
                            }
                        }
                        if (ip == null)
                        {
                            return false;
                        }
                    }

                    listenerSocket = new Socket(
                        AddressFamily.InterNetwork,
                        SocketType.Stream,
                        ProtocolType.Tcp);

                    listenerSocket.Bind(ip);
                    listenerSocket.Listen(numAcceptBacklog);

                    WriteLog(TraceEventType.Information, "Start server -- [" + ip.Address + ":" + port + "]");
                    WriteLog(TraceEventType.Verbose, "InitServer");
                }
                catch (Exception e)
                {
                    WriteLog(TraceEventType.Critical, "InitSocket: " + e.Message);
                    return false;
                }

                return true;
            }

            // Db初期化
            public void InitDb(String name)
            {
                if (mainDb != null)
                {
                    return;
                }
                mainDb = new Sqlite3.sqlite3();

                int rc = Sqlite3.sqlite3_open_v2(
                    name,
                    out mainDb,
                    Sqlite3.SQLITE_OPEN_READWRITE | Sqlite3.SQLITE_OPEN_CREATE | Sqlite3.SQLITE_OPEN_SHAREDCACHE,
                    "");
                if (rc != Sqlite3.SQLITE_OK)
                {
                    WriteLog(TraceEventType.Critical, "Database initialization failed.");
                    Sqlite3.sqlite3_close(mainDb);
                    return;
                }

                // トランザクション
                Sqlite3.sqlite3_exec(mainDb, "BEGIN", 0, 0, 0);

                // Roomテーブル生成
                Sqlite3.sqlite3_exec(
                    mainDb,
                    "CREATE TABLE room (" +
                    "id INT PRIMARY KEY," +
                    "member_num INT," +
                    "member_max INT," +
                    "name TINYTEXT NOT NULL," +
                    "comment TEXT)",
                    0, 0, 0);

                // userテーブル生成
                Sqlite3.sqlite3_exec(
                    mainDb,
                    "CREATE TABLE user (" +
                    "id INT PRIMARY KEY," +
                    "room_id INT," +
                    "name TINYTEXT NOT NULL," +
                    "comment TEXT)",
                    0, 0, 0);

                // トランザクション
                Sqlite3.sqlite3_exec(mainDb, "COMMIT", 0, 0, 0);

                WriteLog(TraceEventType.Verbose, "InitDb");
            }

            // 処理
            public void Execute()
            {
                try
                {
                    WriteLog(TraceEventType.Information, "Waiting for a connection...");

                    IAsyncResult ar = null;
                    bool timeOut = false;
                    while (!stopListening)
                    {
                        if (!timeOut)
                        {
                            acceptDone.Reset();
                            ar = listenerSocket.BeginAccept(new AsyncCallback(AcceptCallback), this);
                        }
                        timeOut = acceptDone.WaitOne(AcceptTimeOut);
                    }
                }
                catch (Exception e)
                {
                    WriteLog(TraceEventType.Error, "Execute: " + e.Message);
                }
            }

            // Dbを閉じる
            public void CloseDb()
            {
                try
                {
                    if (mainDb != null)
                    {
                        Sqlite3.sqlite3_close(mainDb);
                        mainDb = null;
                    }
                }
                catch (Exception e)
                {
                    WriteLog(TraceEventType.Error, "CloseDb: " + e.Message);
                }
                WriteLog(TraceEventType.Verbose, "CloseDb");
            }

            // ソケットを閉じる
            public void CloseSocket()
            {
                try
                {
                    if (listenerSocket != null)
                    {
                        if (listenerSocket.Connected)
                        {
                            listenerSocket.Disconnect(false);
                            listenerSocket.Shutdown(SocketShutdown.Both);
                        }
                    }
                    WriteLog(TraceEventType.Information, "Close server");
                }
                catch (Exception e)
                {
                    WriteLog(TraceEventType.Error, "CloseSocket: " + e.Message);
                }
                finally
                {
                    if (listenerSocket != null)
                    {
                        listenerSocket.Close();
                        listenerSocket = null;
                    }
                }
                WriteLog(TraceEventType.Verbose, "CloseSocket");
            }

            // ログ出力
            private void WriteLog(TraceEventType ev, String msg)
            {
                if (traceLog != null)
                {
                    traceLog.Append(ev, msg);
                }
            }

            // リセット
            public void Reset()
            {
                stopListening = false;
            }

            // 処理の終了をリクエスト
            public void RequestTerminate()
            {
                stopListening = true;
            }

            // 接続処理用コールバック
            private static void AcceptCallback(IAsyncResult ar)
            {
                AcceptWorker server = (AcceptWorker)ar.AsyncState;
                Socket listener = server.listenerSocket;
                Socket worker = null;

                if (listener == null)
                {
                    return;
                }

                try
                {
                    worker = listener.EndAccept(ar);

                    // クライアント用ソケットをリストに追加
                    int id = Interlocked.Increment(ref server.workerSocketId);
                    server.workerSocketHash.Add(id, worker);

                    // 接続処理を再開
                    server.acceptDone.Set();

                    // ログにクライアント情報を出力
                    {
                        IPEndPoint remoteEP = (IPEndPoint)worker.RemoteEndPoint;
                        server.WriteLog(TraceEventType.Information, "Connected from -- [" + remoteEP.Address + "]");
                    }

                    ServerChildJob clientJob = null;
                    {
                        ServerChildJobState state = new ServerChildJobState();
                        state.socketID = id;
                        state.workerSocket = worker;
                        state.traceLog = server.traceLog;
                        state.mainDb = server.mainDb;
                        clientJob = new ServerChildJob(state);
                    }

                    // クライアント応対処理
                    while (!clientJob.IsShutdown())
                    {
                        clientJob.Update();
                        Thread.Sleep(0);
                    }

                    // クライアント用ソケットをから削除
                    server.workerSocketHash.Remove(id);
                }
                catch (Exception e)
                {
                    server.WriteLog(TraceEventType.Error, "AcceptCallback: " + e.Message);
                }
                finally
                {
                    // 終了処理
                    if (worker != null)
                    {
                        if (worker.Connected)
                        {
                            worker.Shutdown(SocketShutdown.Both);
                        }
                        worker.Close();
                    }
                }
            }
        }
    }
}
