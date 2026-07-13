using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DatabaseExplorer.Core.Models;
using GalaSoft.MvvmLight;

namespace DatabaseExplorer.ViewModels;

public sealed partial class TreeNodeViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public TreeNodeViewModel(string name, DatabaseObjectType nodeType, TreeNodeViewModel? parent = null, object? tag = null)
    {
        Name = name;
        NodeType = nodeType;
        Parent = parent;
        Tag = tag;
    }

    public string Name { get; }

    public DatabaseObjectType NodeType { get; }

    public TreeNodeViewModel? Parent { get; }

    public object? Tag { get; }

    public ObservableCollection<TreeNodeViewModel> Children { get; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    public string? SchemaName => NodeType switch
    {
        DatabaseObjectType.Schema => Name,
        _ => Parent?.SchemaName
    };

    public bool IsDataObject => NodeType is DatabaseObjectType.Table or DatabaseObjectType.View;
}
