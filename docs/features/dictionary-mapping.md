# Dictionaries & `ExpandoObject`

Objects map to and from string-keyed dictionaries without any configuration.

```csharp
var form = new Dictionary<string, object?>
{
    ["name"] = "Margaret Hamilton",
    ["age"] = "33",
    ["address"] = new Dictionary<string, object?> { ["city"] = "Cambridge" }
};

FormSubmission submission = engine.Map<Dictionary<string, object?>, FormSubmission>(form);
ExpandoObject expando = engine.Map<FormSubmission, ExpandoObject>(submission);

dynamic input = new ExpandoObject();
input.Name = "Ann";
Contact contact = engine.Map<Contact>((object)input);
```

## Dictionary to object

* Works for any source that implements `IDictionary<string, object>` or `IReadOnlyDictionary<string, object>`, including `ExpandoObject`.
* Each destination member reads the key with its name, ignoring case. A missing key leaves the member alone.
* Values are converted to the member type. A nested dictionary becomes a nested object, and a list becomes a collection.
* `Skip`, `UseValue` and `[SourceMember]` work. Other member options are not used.
* The destination needs a parameterless constructor.

## Object to dictionary

* Works for `Dictionary<string, object>`, `IDictionary<string, object>`, `IReadOnlyDictionary<string, object>` and `ExpandoObject` destinations.
* Every readable public property and field becomes a key. Nested objects are stored as they are.
* Mapping onto an existing dictionary sets the keys without clearing it.

Validation skips maps from or to dictionaries, because the keys are only known at run time. The analyzer skips them too.

## AutoMapper Migration Guide

| AutoMapper | AutoMapSharp |
|---|---|
| `mapper.Map<Contact>(dictionary)` | `engine.Map<Contact>((object)dictionary)` or `engine.Map<Dictionary<string, object?>, Contact>(dictionary)` |
| `mapper.Map<ExpandoObject>(contact)` | `engine.Map<Contact, ExpandoObject>(contact)` |
