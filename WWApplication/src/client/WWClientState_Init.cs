using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WW
{
    public class WWClientState_Init : IFSMInterface
    {
        public override void Entry(object context)
        {
        }

        public override bool Execute(object context)
        {
            return true;
        }

        public override void Exit(object context)
        {
        }
    }
}
