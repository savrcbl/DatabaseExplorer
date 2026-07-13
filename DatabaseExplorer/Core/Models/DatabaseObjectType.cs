using System;
using System.Collections.Generic;
using System.Text;

namespace DatabaseExplorer.Core.Models
{
    public enum DatabaseObjectType
    {
        Database,
        Schema,
        TablesFolder,
        ViewsFolder,
        ProceduresFolder,
        Table,
        View,
        Procedure,
        Column
    }
}
