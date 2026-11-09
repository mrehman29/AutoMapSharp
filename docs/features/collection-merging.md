# Collection Merging (`AutoMapSharp.Collections`)

By default, mapping onto an existing collection clears it and adds newly mapped items. With an equality comparison, it merges instead.

```bash
dotnet add package AutoMapSharp.Collections
```

```csharp
using AutoMapSharp.Collections;

DefineMap<CartLineDto, CartLine>()
    .EqualityComparison((dto, line) => dto.Sku == line.Sku);
DefineMap<CartDto, Cart>();

engine.Map(cartDto, trackedCart);
```

When `cartDto.Lines` is mapped onto `trackedCart.Lines`:

| Item | What happens |
|---|---|
| A destination item that matches a source item | Updated in place. The same instance is kept |
| A source item without a match | Mapped to a new item and added at the end |
| A destination item without a match | Removed |

This keeps EF Core tracked entities, so `SaveChanges` issues the right `UPDATE`, `INSERT` and `DELETE` statements.

* The comparison is set on the element map, and applies wherever that element type is mapped onto an existing mutable collection.
* Without an existing collection, or when it is read-only or an array, a new collection is created as usual.
* Each item is matched at most once. A destination item is matched to the first source item it equals.

## AutoMapper Migration Guide

| AutoMapper.Collection | AutoMapSharp.Collections |
|---|---|
| `cfg.AddCollectionMappers()` | Not needed |
| `.EqualityComparison((dto, entity) => dto.Id == entity.Id)` | Same |
| `AutoMapper.Collection.EntityFrameworkCore` | Not needed. Merging works on any mutable collection |
