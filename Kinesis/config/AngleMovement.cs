using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Research.Kinect.Nui;

namespace Kinect.Extensions
{
    public static class CameraExtensions
    {
        public static bool TryToSetAngle(this Camera cam, int angle)
        {
            try
            {
                cam.ElevationAngle = angle;
                return true;
            }
            catch(Exception e)
            {
                return false;
            }
        }
    }
}
