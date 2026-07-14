namespace DatabaseExplorer.Core.Models;

/// <summary>
/// Identifies the kind of database object a tree node represents.
/// Used to choose icons, templates, and the correct metadata-loading behavior.
/// </summary>
public enum DatabaseObjectType
{
    /// <summary>The root node representing the connected database.</summary>
    Database,

    /// <summary>A schema/namespace grouping tables, views, and procedures.</summary>
    Schema,

    /// <summary>A container node grouping all tables within a schema.</summary>
    TablesFolder,

    /// <summary>A container node grouping all views within a schema.</summary>
    ViewsFolder,

    /// <summary>A container node grouping all stored procedures within a schema.</summary>
    ProceduresFolder,

    /// <summary>A physical table.</summary>
    Table,

    /// <summary>A database view.</summary>
    View,

    /// <summary>A stored procedure.</summary>
    Procedure,

    /// <summary>A column belonging to a table or view (used for metadata display).</summary>
    Column
}
