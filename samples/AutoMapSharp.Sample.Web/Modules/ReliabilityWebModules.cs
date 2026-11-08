using AutoMapSharp.Sample.Web.Models;

namespace AutoMapSharp.Sample.Web.Modules;

/// <summary>
/// Mappings for the Reliability &amp; Parity demo pages.
/// Code between snippet markers is shown on the demo pages, so keep it readable.
/// </summary>
public class ReliabilityWebModule : MappingModule
{
    public ReliabilityWebModule()
    {
        // <snippet:NullableValues>
        // No configuration needed. Nullable values, structs and object-typed
        // values are copied as they are.
        DefineMap<ShipmentRecord, ShipmentViewModel>();
        // </snippet:NullableValues>

        // <snippet:SafeParameters>
        // A local captured by MapSource acts as a projection parameter.
        // Its value here is only a placeholder. The real value is passed to ProjectOnto.
        int minQuantity = 0;
        DefineMap<DbOrder, BulkOrderSummaryDto>()
            .ForProperty(d => d.BulkLineCount,
                opt => opt.MapSource(s => s.Items.Count(i => i.Quantity >= minQuantity)));
        // </snippet:SafeParameters>

        // <snippet:RecordsNested>
        // Constructor parameters get the same mapping as properties:
        // flattening (CustomerName), nested records and collections.
        DefineMap<AddressEntity, AddressRecord>();
        DefineMap<OrderLineEntity, OrderLineRecord>();
        DefineMap<PurchaseOrderEntity, PurchaseOrderRecord>();
        // </snippet:RecordsNested>

        // <snippet:ReverseUnflatten>
        // The forward map flattens Contact.Email into ContactEmail.
        // ReverseMap unflattens it again and creates Contact when it is missing.
        DefineMap<CustomerAccount, CustomerAccountForm>().ReverseMap();
        // </snippet:ReverseUnflatten>

        // <snippet:EnumByName>
        // No configuration needed. Enums are matched by name, ignoring case.
        // A name that does not exist in the destination falls back to the numeric value.
        DefineMap<LegacyTicket, TicketView>();
        // </snippet:EnumByName>

        // <snippet:CollectionShapes>
        // No configuration needed. Each destination collection type gets the right creation strategy.
        DefineMap<TagSource, CollectionShapesView>()
            .ForProperty(d => d.Observable, o => o.MapSource(s => s.Tags))
            .ForProperty(d => d.ReadOnly, o => o.MapSource(s => s.Tags))
            .ForProperty(d => d.Immutable, o => o.MapSource(s => s.Tags))
            .ForProperty(d => d.Sorted, o => o.MapSource(s => s.Tags))
            .ForProperty(d => d.Queue, o => o.MapSource(s => s.Tags))
            .ForProperty(d => d.Set, o => o.MapSource(s => s.Tags))
            .ForProperty(d => d.Linked, o => o.MapSource(s => s.Tags))
            .ForProperty(d => d.SortedScores, o => o.MapSource(s => s.Scores));
        // </snippet:CollectionShapes>

        // <snippet:SharedReferences>
        // PreserveReferences keeps one destination object per source object and destination type.
        DefineMap<Team, TeamDto>().PreserveReferences();
        DefineMap<TeamMember, TeamMemberDto>().PreserveReferences();
        DefineMap<TeamMember, TeamMemberCard>().PreserveReferences();
        // </snippet:SharedReferences>

        // <snippet:ScopedResolver>
        // RequestIdResolver depends on the scoped RequestTrace service.
        // IMapEngine is resolved per request, so the resolver gets that request's instance.
        DefineMap<AuditEntry, AuditEntryView>()
            .ForProperty(d => d.RequestId, o => o.MapWith<RequestIdResolver>());
        // </snippet:ScopedResolver>
    }
}
