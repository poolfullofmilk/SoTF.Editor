using System.Collections;

namespace SoTF.Editor.Data.Models;

public class ItemList(IEnumerable<Item> items) : IEnumerable<Item>
{
    private readonly Dictionary<int, Item> _items = items.ToDictionary(item => item.Id);

    public IEnumerator<Item> GetEnumerator()
    {
        return _items.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public Item? GetItem(int id)
    {
        return _items.GetValueOrDefault(id);
    }

    public IEnumerable<Item>? GetItems(IEnumerable<int>? itemIds)
    {
        return itemIds?.Select(GetItem).OfType<Item>().ToList();
    }
}
