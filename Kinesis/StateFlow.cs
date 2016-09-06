using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kinesis
{
    class StateFlow
    {
        private string state = null;
        private List<Func<string, object>> listeners = new List<Func<string, object>>();

        public string getState()
        {
            return state;
        }

        public void subscribe(Func<string, object> f)
        {
            listeners.Add(f);
        }

        public void dispatch(string action)
        {
            state = action;
            foreach(var listener in listeners)
            {
                listener(action);
            }
        }
    }
}
