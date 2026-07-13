using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DatabaseExplorer.Core.Models;
using GalaSoft.MvvmLight;

namespace DatabaseExplorer.ViewModels;

/// <summary>
/// Represents a single node in the left-hand database object tree: the database root,
/// a schema, a folder grouping (Tables/Views/Procedures), or a leaf object (table, view,
/// or procedure). <see cref="Tag"/> carries the underlying model for leaf nodes so the
/// owning view model can react to selection without re-parsing display text.
/// </summary>
public sealed partial class TreeNodeViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public TreeNodeViewModel(string name, DatabaseObjectType nodeType, TreeNodeViewModel? parent = null, object? tag = null)
    {
        Name = name;
        NodeType = nodeType;
        Parent = parent;
        Tag = tag;
    }

    /// <summary>The display name shown in the tree.</summary>
    public string Name { get; }

    /// <summary>The kind of object this node represents; drives icon and behavior.</summary>
    public DatabaseObjectType NodeType { get; }

    /// <summary>The parent node, or null for the root database node.</summary>
    public TreeNodeViewModel? Parent { get; }

    /// <summary>
    /// The underlying model (<see cref="TableInfo"/>, <see cref="ViewInfo"/>, or
    /// <see cref="ProcedureInfo"/>) for leaf nodes; null for the database/schema/folder nodes.
    /// </summary>
    public object? Tag { get; }

    /// <summary>Child nodes, populated eagerly during the metadata scan.</summary>
    public ObservableCollection<TreeNodeViewModel> Children { get; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>The schema this node belongs to (itself, for schema nodes).</summary>
    public string? SchemaName => NodeType switch
    {
        DatabaseObjectType.Schema => Name,
        _ => Parent?.SchemaName
    };

    /// <summary>True for nodes that represent a queryable/data-bearing object.</summary>
    public bool IsDataObject => NodeType is DatabaseObjectType.Table or DatabaseObjectType.View;
}
