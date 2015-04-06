using System;
using System.Collections.Generic;

namespace EHaskins.Utils.Web
{
    public interface IScriptSource
    {
        IEnumerable<string> GetScripts();
    }
}