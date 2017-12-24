﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorzCore.Parser
{
    class BaseClosure : Closure
    {
        private EAParser enclosing;
        public BaseClosure(EAParser enclosing)
        {
            this.enclosing = enclosing;
        }
        public override bool HasLocalLabel(string label)
        {
            return label.ToUpper() == "CURRENTOFFSET" || base.HasLocalLabel(label);
        }
        public override int GetLabel(string label)
        {
            if (label == "CURRENTOFFSET")
                return enclosing.CurrentOffset;
            else
                return base.GetLabel(label);
        }
    }
}
