using System;

namespace Xilium.CefGlue.Broker.Admin
{
    internal sealed class WorkerInfo
    {
        public string TenantId { get; set; }
        public string PageId { get; set; }
        public string Address { get; set; }
        public string Status { get; set; }
        public DateTime LastHeartbeatUtc { get; set; }
        public double Cpu { get; set; }
        public double Encoder { get; set; }
        public int Sessions { get; set; }
        public DateTime? CreatedUtc { get; set; }
        public string ClientIp { get; set; }
        public string ClientOs { get; set; }
    }
}
