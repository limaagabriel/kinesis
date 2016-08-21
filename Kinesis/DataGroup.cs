using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Kinect;

namespace Kinesis
{
    class DataGroup
    {
        private int port;
        private string host;
        private Skeleton s;

        public DataGroup(int port, string host, Skeleton s)
        {
            this.port = port;
            this.host = host;
            this.s = s;
        }

        public Skeleton getSkeleton()
        {
            return s;
        }

        public int getPort()
        {
            return port;
        }

        public string getHost()
        {
            return host;
        }
    }
}
