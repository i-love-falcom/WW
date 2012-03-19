using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Community.CsharpSqlite;
using Community.CsharpSqlite.SQLiteClient;

namespace WW
{
    class WWServerState_Init : IFSMInterface
    {
        // 入室処理
        public override void Entry(object context)
        {
        }

        // 実行
        public override bool Execute(object context)
        {
            ServerChildJob job = (ServerChildJob)context;

            if (job.recvBuffer.Count > 0)
            {
                foreach (ArraySegment<byte> seg in job.recvBuffer)
                {
                    byte[] bytes = seg.Array;
                    if (WWProtocolV1Helper.IsCmdV1(bytes))
                    {
                        if (WWProtocolV1Helper.GetCmd(bytes) == WWProtocolV1Helper.Cmd.CMD_HELLO)
                        {
                            uint packetNumber;
                            packetNumber = (uint)bytes[2] << 24;
                            packetNumber |= (uint)bytes[3] << 16;
                            packetNumber |= (uint)bytes[4] << 8;
                            packetNumber |= (uint)bytes[5];

                            // クライアントのパケット番号を取得
                            job.packetNum = WWProtocolV1Helper.GetPacketNumber(bytes);

                            // OKパケットをクライアントへ送信
                            byte[] sendData = WWProtocolV1Helper.CreateOkPacket(++job.packetNum);
                            job.sendBuffer.Add(new ArraySegment<byte>(sendData));
                        }
                        job.recvBuffer.Remove(seg);
                    }
                }
            }

            return true;
        }

        // 退室処理
        public override void Exit(object context)
        {
        }


        

        // ルーム一覧リクエスト待ち受け
        private void Exec_WaitReqRoomList(ref ServerChildJob job)
        {
            if (job.recvBuffer.Count > 0)
            {
                foreach (ArraySegment<byte> seg in job.recvBuffer)
                {
                    byte[] bytes = seg.Array;
                    if (WWProtocolV1Helper.IsCmdV1(bytes) &&
                        WWProtocolV1Helper.GetCmd(bytes) == WWProtocolV1Helper.Cmd.CMD_REQ_ROOM_LIST
                    )
                    {
                        try
                        {
                            Sqlite3.sqlite3_exec(job.mainDb, "BEGIN", 0, 0, 0);
                            int rc = Sqlite3.sqlite3_exec(job.mainDb, "SELECT * FROM room;", 0, 0, 0);
                            if (rc != Sqlite3.SQLITE_OK)
                            {
                                Exception e = new Exception("sqlite3_exec error.");
                                throw e;
                            }
                            Sqlite3.sqlite3_exec(job.mainDb, "COMMIT", 0, 0, 0);
                            

                            // 処理したのでバッファから削除
                            job.recvBuffer.Remove(seg);
                        }
                        catch (Exception e)
                        {
                            job.WriteLog(TraceEventType.Error, e.Message);

                            // 途中でエラー発生ならロールバック
                            Sqlite3.sqlite3_exec(job.mainDb, "ROLLBACK", 0, 0, 0);
                        }
                    }
                }
            }
        }

        // ルーム一覧取得コールバック
        public static void ReqRoomListCallback(object callbackArg, long argc, object p2, object p3)
        {
        }
    }
}
