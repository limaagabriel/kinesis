using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Kinect;

namespace Kinesis
{
    class SkeletonJobResult
    {
        private Skeleton skeleton;
        private string content;
         
        public Skeleton Skeleton
        {
            get { return skeleton; }
        }

        public string Content
        {
            get { return content; }
        }

        public SkeletonJobResult(Skeleton s, string c)
        {
            skeleton = s;
            content = c;
        }
    }
}
