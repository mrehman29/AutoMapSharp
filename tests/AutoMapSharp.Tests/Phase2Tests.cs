using System;
using AutoMapSharp.Conventions;
using Xunit;

namespace AutoMapSharp.Tests;

public class Phase2Tests
{
    #region Models
    public enum Status
    {
        Pending = 0,
        Active = 1,
        Archived = 2
    }

    public class Address
    {
        public string City { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
    }

    public class Customer
    {
        public string Name { get; set; } = string.Empty;
        public Address? Address { get; set; }
    }

    public class Order
    {
        public int Id { get; set; }
        public Customer? Customer { get; set; }
        public Status OrderStatus { get; set; }
        public Guid TrackingId { get; set; }
        public DateTime OrderDate { get; set; }

        public string GetSpecialNotes() => "Confidential";
    }

    public class FlatOrderDto
    {
        public int Id { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerAddressCity { get; set; }
        public string? SpecialNotes { get; set; }
        public string? OrderStatus { get; set; } // Enum -> String
        public string? TrackingId { get; set; } // Guid -> String
        public DateTimeOffset OrderDate { get; set; } // DateTime -> DateTimeOffset
    }

    public class SnakeCaseSource
    {
        public string? first_name { get; set; }
        public string? last_name { get; set; }
        public int user_age { get; set; }
    }

    public class PascalCaseDestination
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int UserAge { get; set; }
    }

    public class PrivateSetterModel
    {
        public string? SecretCode { get; private set; }

        public void SetInitial(string code) => SecretCode = code;
    }

    public class SourceWithPrefixesAndSuffixes
    {
        public string? m_Title { get; set; }
        public string? CategoryModel { get; set; }
    }

    public class CleanDestination
    {
        public string? Title { get; set; }
        public string? Category { get; set; }
    }

    public record PersonRecord(string Name, int Age);

    public class PersonEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
    }
    #endregion

    #region Modules
    public class FlatOrderModule : MappingModule
    {
        public FlatOrderModule()
        {
            DefineMap<Order, FlatOrderDto>();
        }
    }

    public class SnakeToPascalModule : MappingModule
    {
        public SnakeToPascalModule()
        {
            SourceNamingConvention = SnakeCaseNamingConvention.Instance;
            DestinationNamingConvention = PascalCaseNamingConvention.Instance;
            DefineMap<SnakeCaseSource, PascalCaseDestination>();
        }
    }

    public class PrefixSuffixModule : MappingModule
    {
        public PrefixSuffixModule()
        {
            RecognizePrefixes("m_");
            RecognizeSuffixes("Model");
            DefineMap<SourceWithPrefixesAndSuffixes, CleanDestination>();
        }
    }

    public class PrivateSetterModule : MappingModule
    {
        public PrivateSetterModule()
        {
            DefineMap<PascalCaseDestination, PrivateSetterModel>()
                .ForProperty(d => d.SecretCode, opt => opt.MapSource(s => s.FirstName));
        }
    }

    public class RecordModule : MappingModule
    {
        public RecordModule()
        {
            DefineMap<PersonEntity, PersonRecord>();
        }
    }
    #endregion

    [Fact]
    public void Task2_1_CaseInsensitiveOrdinalMatching_MapsProperties()
    {
        var module = new FlatOrderModule();
        var engine = new MapEngine(new[] { module });
        var order = new Order { Id = 42 };

        var dto = engine.Map<Order, FlatOrderDto>(order);

        Assert.Equal(42, dto.Id);
    }

    [Fact]
    public void Task2_2_DeepFlattening_MapsNestedPropertiesAndMethods()
    {
        var module = new FlatOrderModule();
        var engine = new MapEngine(new[] { module });
        var order = new Order
        {
            Id = 1,
            Customer = new Customer
            {
                Name = "John Doe",
                Address = new Address { City = "Seattle", ZipCode = "98101" }
            }
        };

        var dto = engine.Map<Order, FlatOrderDto>(order);

        Assert.Equal("John Doe", dto.CustomerName);
        Assert.Equal("Seattle", dto.CustomerAddressCity);
        Assert.Equal("Confidential", dto.SpecialNotes);
    }

    [Fact]
    public void Task2_2_DeepFlattening_WithNullIntermediate_DoesNotThrow()
    {
        var module = new FlatOrderModule();
        var engine = new MapEngine(new[] { module });
        var order = new Order
        {
            Id = 2,
            Customer = null // Null intermediate object
        };

        var dto = engine.Map<Order, FlatOrderDto>(order);

        Assert.Null(dto.CustomerName);
        Assert.Null(dto.CustomerAddressCity);
    }

    [Fact]
    public void Task2_3_NamingConventions_MapsSnakeCaseToPascalCase()
    {
        var module = new SnakeToPascalModule();
        var engine = new MapEngine(new[] { module });
        var src = new SnakeCaseSource
        {
            first_name = "Jane",
            last_name = "Smith",
            user_age = 29
        };

        var dest = engine.Map<SnakeCaseSource, PascalCaseDestination>(src);

        Assert.Equal("Jane", dest.FirstName);
        Assert.Equal("Smith", dest.LastName);
        Assert.Equal(29, dest.UserAge);
    }

    [Fact]
    public void Task2_3_PrefixAndSuffixStripping_MapsCleanly()
    {
        var module = new PrefixSuffixModule();
        var engine = new MapEngine(new[] { module });
        var src = new SourceWithPrefixesAndSuffixes
        {
            m_Title = "Senior Architect",
            CategoryModel = "Engineering"
        };

        var dest = engine.Map<SourceWithPrefixesAndSuffixes, CleanDestination>(src);

        Assert.Equal("Senior Architect", dest.Title);
        Assert.Equal("Engineering", dest.Category);
    }

    [Fact]
    public void Task2_4_Conversions_EnumsGuidsAndDates_ConvertSuccessfully()
    {
        var module = new FlatOrderModule();
        var engine = new MapEngine(new[] { module });
        var trackingGuid = Guid.NewGuid();
        var orderDate = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

        var order = new Order
        {
            OrderStatus = Status.Active,
            TrackingId = trackingGuid,
            OrderDate = orderDate
        };

        var dto = engine.Map<Order, FlatOrderDto>(order);

        Assert.Equal("Active", dto.OrderStatus);
        Assert.Equal(trackingGuid.ToString(), dto.TrackingId);
        Assert.Equal(new DateTimeOffset(orderDate), dto.OrderDate);
    }

    [Fact]
    public void Task2_5_ConstructorMapping_MapsToPositionalRecord()
    {
        var module = new RecordModule();
        var engine = new MapEngine(new[] { module });
        var entity = new PersonEntity { Name = "Michael", Age = 35 };

        var record = engine.Map<PersonEntity, PersonRecord>(entity);

        Assert.NotNull(record);
        Assert.Equal("Michael", record.Name);
        Assert.Equal(35, record.Age);
    }

    [Fact]
    public void Task2_6_PrivateSetter_AssignsValueAccurately()
    {
        var module = new PrivateSetterModule();
        var engine = new MapEngine(new[] { module });
        var src = new PascalCaseDestination { FirstName = "TopSecret123" };

        var dest = engine.Map<PascalCaseDestination, PrivateSetterModel>(src);

        Assert.NotNull(dest);
        Assert.Equal("TopSecret123", dest.SecretCode);
    }
}
