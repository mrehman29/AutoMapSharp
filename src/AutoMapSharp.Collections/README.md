# AutoMapSharp.Collections

By default, mapping onto an existing collection clears it and fills it again with new objects. This package lets [AutoMapSharp](https://www.nuget.org/packages/AutoMapSharp) merge the collection instead. It replaces `AutoMapper.Collection`.

That matters most with EF Core. Tracked entities keep their identity, so `SaveChanges` sends the right `UPDATE`, `INSERT` and `DELETE` statements.

## Install

```bash
dotnet add package AutoMapSharp.Collections
```

## Usage

Tell AutoMapSharp how to match a source item with a destination item:

```csharp
using AutoMapSharp.Collections;

DefineMap<CartLineDto, CartLine>()
    .EqualityComparison((dto, line) => dto.Sku == line.Sku);
DefineMap<CartDto, Cart>();
```

Then map onto the existing object as usual:

```csharp
engine.Map(cartDto, trackedCart);
```

When the lines are mapped:

- **Matching items are updated in place.** The same instance stays in the collection.
- **New source items are added.** Each one is mapped to a new destination item.
- **Missing items are removed.** A destination item with no match in the source is taken out.

## Moving from AutoMapper.Collection

- `cfg.AddCollectionMappers()` is not needed.
- `.EqualityComparison(...)` works the same way.
- `AutoMapper.Collection.EntityFrameworkCore` is not needed. Merging works on any mutable collection.

See the [collection merging guide](https://github.com/mrehman29/AutoMapSharp/blob/main/docs/features/collection-merging.md) and the [migration guide](https://github.com/mrehman29/AutoMapSharp/blob/main/MIGRATION_GUIDE.md) for details.
