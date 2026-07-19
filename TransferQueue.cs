using System;
using System.Collections.Generic;

namespace AndroidSideloader
{
    public enum TransferState { Queued, Active, Done, Failed }

    // A single file transfer surfaced in the TransferWindow.
    public class TransferItem
    {
        public string Name;
        public string Kind;          // "SFTP", "Push", "Pull"
        public long TotalBytes;
        public long TransferredBytes;
        public double SpeedMBps;
        public TransferState State = TransferState.Queued;
    }

    // Thread-safe registry of in-flight file transfers. Producers are the transfer
    // workers (background threads); the consumer is the TransferWindow UI timer.
    public static class TransferQueue
    {
        private static readonly object _lock = new object();
        private static readonly List<TransferItem> _items = new List<TransferItem>();

        public static TransferItem Add(string name, string kind, long totalBytes)
        {
            TransferItem item = new TransferItem { Name = name, Kind = kind, TotalBytes = totalBytes };
            lock (_lock)
            {
                _items.Add(item);
            }
            TransferWindow.NotifyActivity();
            return item;
        }

        public static void SetState(TransferItem item, TransferState state)
        {
            if (item == null) return;
            lock (_lock)
            {
                item.State = state;
            }
        }

        public static void Report(TransferItem item, long transferred, double speedMBps)
        {
            if (item == null) return;
            lock (_lock)
            {
                item.TransferredBytes = transferred;
                item.SpeedMBps = speedMBps;
                if (item.State == TransferState.Queued)
                {
                    item.State = TransferState.Active;
                }
            }
        }

        public static List<TransferItem> Snapshot()
        {
            lock (_lock)
            {
                return new List<TransferItem>(_items);
            }
        }

        public static void ClearFinished()
        {
            lock (_lock)
            {
                _items.RemoveAll(i => i.State == TransferState.Done || i.State == TransferState.Failed);
            }
        }
    }
}
