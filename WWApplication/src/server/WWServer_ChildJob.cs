using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using Community.CsharpSqlite;
using Community.CsharpSqlite.SQLiteClient;

namespace WW
{
    // ServerMainJobからServerChildJobに渡すデータ
    public class ServerChildJobState
    {
        public int socketID { get; set; }
        public Socket workerSocket { get; set; }
        public TraceLog traceLog { get; set; }
        public Sqlite3.sqlite3 mainDb { get; set; }
    };

    // クライアント応対処理
    public class ServerChildJob
    {
        // 状態
        private enum State
        {
            STATE_INIT = 0,
            STATE_SHUTDOWN,

            STATE_COUNT
        }
        private int stateID;
        private FSM fsm = null;
        private IFSMInterface[] stateArray = null;

        // ログ
        public TraceLog traceLog = null;
        
        // ソケット他
        public int socketID { get; set; }
        public Socket socket = null;

        // Database
        public Sqlite3.sqlite3 mainDb = null;
        
        // クライアント情報
        public int roomID = -1;
        public String userName;
        public String userComment;
        public String roomName;
        public uint packetNum;

        // 送受信用
        public List<ArraySegment<byte>> sendBuffer = new List<ArraySegment<byte>>();
        public List<ArraySegment<byte>> recvBuffer = new List<ArraySegment<byte>>();
        

        public ServerChildJob(ServerChildJobState st)
        {
            socketID = st.socketID;
            socket = st.workerSocket;
            traceLog = st.traceLog;
            mainDb = st.mainDb;

            stateArray = new IFSMInterface[(int)State.STATE_COUNT];
            stateArray[(int)State.STATE_INIT] = new WWClientState_Init();
            stateArray[(int)State.STATE_SHUTDOWN] = new WWClientState_Shutdown();

            stateID = (int)State.STATE_INIT;
            fsm = new FSM(stateArray[stateID]);
        }
        
        // 更新処理
        public void Update()
        {
            try
            {
                // 可能なら受信
                if (socket.Available > 0)
                {
                    socket.Receive(recvBuffer, SocketFlags.None);
                }

                // 状態処理
                fsm.ExecuteState(this);
                
                // 可能なら送信
                if (sendBuffer.Count > 0)
                {
                    socket.Send(sendBuffer, SocketFlags.None);
                }
            }
            catch (Exception e)
            {
                traceLog.Append(TraceEventType.Error, e.Message);
            }

            // 接続が切れていたら終了処理
            if (stateID != (uint)State.STATE_SHUTDOWN && !socket.Connected)
            {
                // ここに終了処理を入れる
                socket.Disconnect(true);
            }
        }

        // シャットダウン状態かどうか
        public bool IsShutdown()
        {
            return (stateID == (uint)State.STATE_SHUTDOWN);
        }

        // ログ出力
        public void WriteLog(TraceEventType ev, String msg)
        {
            traceLog.Append(ev, msg);
        }
    };

}
