using System;
using System.Linq;
using AutoMapSharp;
using AutoMapSharp.Configuration;
using Xunit;

namespace AutoMapSharp.Tests;

public class Phase3Tests
{
    public class UserSource
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PasswordHash { get; set; }
        public string? InternalNotes { get; set; }
        public int Age { get; set; }
        public string? Bio { get; set; }
        public string? MessyInput { get; set; }
    }

    public class UserDto
    {
        public string? FullName { get; set; }
        public string? PasswordHash { get; set; }
        public string? InternalNotes { get; set; }
        public string? TenantId { get; set; }
        public int Age { get; set; }
        public string? Bio { get; set; }
        public string? MessyInput { get; set; }
    }

    public class UserModule : MappingModule
    {
        public UserModule()
        {
            ValueTransformers.Add<string>(s => s != null ? s.Trim() : string.Empty);

            DefineMap<UserSource, UserDto>()
                .ForProperty(d => d.FullName, opt => opt.MapSource(s => s.FirstName + " " + s.LastName))
                .ForProperty(d => d.PasswordHash, opt => opt.Skip())
                .ForProperty(d => d.InternalNotes, opt => opt.Skip())
                .ForProperty(d => d.TenantId, opt => opt.UseValue("TENANT-GLOBAL"))
                .ForProperty(d => d.Age, opt => opt.If(s => s.Age >= 18))
                .ForProperty(d => d.Bio, opt => opt.Fallback("No bio provided."))
                .ReverseMap()
                .ForProperty(s => s.FirstName, opt => opt.MapSource(d => ExtractFirst(d.FullName)))
                .ForProperty(s => s.LastName, opt => opt.MapSource(d => ExtractLast(d.FullName)));
        }

        private static string? ExtractFirst(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;
            var parts = fullName.Split(' ');
            return parts.Length > 0 ? parts[0] : null;
        }

        private static string? ExtractLast(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;
            var parts = fullName.Split(' ');
            return parts.Length > 1 ? parts[1] : null;
        }
    }

    [Fact]
    public void Task3_1_FluentMappingDefinition_ChainsSuccessfully()
    {
        var module = new UserModule();
        Assert.Single(module.TypeMaps, t => t.SourceType == typeof(UserSource));
        Assert.Single(module.TypeMaps, t => t.SourceType == typeof(UserDto)); // Reverse map registered
    }

    [Fact]
    public void Task3_2_PropertySkipping_And_ConstantValues()
    {
        var engine = new MapEngine(new[] { new UserModule() });
        var source = new UserSource
        {
            FirstName = "Bruce",
            LastName = "Wayne",
            PasswordHash = "SuperSecretHash123",
            InternalNotes = "Internal VIP note",
            Age = 35
        };

        var dto = engine.Map<UserSource, UserDto>(source);

        Assert.Null(dto.PasswordHash);
        Assert.Null(dto.InternalNotes);
        Assert.Equal("TENANT-GLOBAL", dto.TenantId);

        // Verify skipped property on existing destination remains untouched
        var existingDto = new UserDto
        {
            PasswordHash = "PreservedHash",
            InternalNotes = "PreservedNotes",
            TenantId = "OLD-TENANT"
        };

        engine.Map(source, existingDto);
        Assert.Equal("PreservedHash", existingDto.PasswordHash);
        Assert.Equal("PreservedNotes", existingDto.InternalNotes);
        Assert.Equal("TENANT-GLOBAL", existingDto.TenantId);
    }

    [Fact]
    public void Task3_3_MapSource_ComputedExpression()
    {
        var engine = new MapEngine(new[] { new UserModule() });
        var source = new UserSource
        {
            FirstName = "Clark",
            LastName = "Kent",
            Age = 30
        };

        var dto = engine.Map<UserSource, UserDto>(source);

        Assert.Equal("Clark Kent", dto.FullName);
    }

    [Fact]
    public void Task3_4_Conditionals_And_Fallbacks()
    {
        var engine = new MapEngine(new[] { new UserModule() });

        // Under 18: Age condition fails, property untouched
        var minor = new UserSource
        {
            FirstName = "Dick",
            LastName = "Grayson",
            Age = 15,
            Bio = null // Fallback should apply
        };

        var existingMinorDto = new UserDto
        {
            Age = 99 // Should remain 99 because condition s.Age >= 18 is false
        };

        engine.Map(minor, existingMinorDto);
        Assert.Equal(99, existingMinorDto.Age);
        Assert.Equal("No bio provided.", existingMinorDto.Bio);

        // Over 18: Age condition passes
        var adult = new UserSource
        {
            FirstName = "Diana",
            LastName = "Prince",
            Age = 28,
            Bio = "Amazonian Princess"
        };

        var adultDto = engine.Map<UserSource, UserDto>(adult);
        Assert.Equal(28, adultDto.Age);
        Assert.Equal("Amazonian Princess", adultDto.Bio);
    }

    [Fact]
    public void Task3_5_Bidirectional_ReverseMap()
    {
        var engine = new MapEngine(new[] { new UserModule() });
        var dto = new UserDto
        {
            FullName = "Barry Allen",
            Age = 25
        };

        var source = engine.Map<UserDto, UserSource>(dto);

        Assert.Equal("Barry", source.FirstName);
        Assert.Equal("Allen", source.LastName);
    }

    [Fact]
    public void Task3_6_ValueTransformers_SanitizesStrings()
    {
        var engine = new MapEngine(new[] { new UserModule() });
        var source = new UserSource
        {
            Bio = "   A protector of the deep seas.   \t   ",
            MessyInput = "   Dirty text with whitespace   \t\n   ",
            Age = 32
        };

        var dto = engine.Map<UserSource, UserDto>(source);

        Assert.Equal("A protector of the deep seas.", dto.Bio);
        Assert.Equal("Dirty text with whitespace", dto.MessyInput);
    }
}
