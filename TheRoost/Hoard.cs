using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using SecretHistories.Fucine;
using SecretHistories.Fucine.DataImport;

using UnityEngine;

namespace TheRoost.Hoard
{

    public struct FucineInt
    {
        string expression;
        public FucineInt(string expression) { this.expression = expression; }
        public static implicit operator int(FucineInt fucinevalue) { return int.Parse(fucinevalue.expression); }
    }


}