using OpenPortal.Content.Domain;
using OpenPortal.Content.Domain.Projects;

namespace OpenPortal.Tests.Unit.Content;

public sealed class ProjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static Project NewProject(string name = "Analytical Engine", string slug = "analytical-engine") =>
        new(Guid.NewGuid(), name, slug, Now);

    private static Technology NewTechnology(string name)
    {
        var trimmed = name.Trim();
        return new Technology(Guid.NewGuid(), trimmed, Technology.Normalise(trimmed));
    }

    [Fact]
    public void Constructor_normalises_the_slug_but_preserves_the_name_casing()
    {
        var project = NewProject("Analytical Engine", "  Analytical-Engine  ");

        project.Name.ShouldBe("Analytical Engine");
        project.Slug.ShouldBe("analytical-engine");
    }

    [Fact]
    public void Constructor_is_unpublished_and_harvests_nothing_by_default()
    {
        var project = NewProject();

        project.IsPublished.ShouldBeFalse();
        project.Position.ShouldBeNull();
        project.StartedOn.ShouldBeNull();
        project.CompletedOn.ShouldBeNull();
        project.Technologies.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_a_blank_name(string name)
    {
        Should.Throw<ArgumentException>(() => NewProject(name));
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("has/slash")]
    [InlineData("")]
    public void Constructor_rejects_a_slug_outside_the_url_safe_alphabet(string slug)
    {
        Should.Throw<ArgumentException>(() => NewProject(slug: slug));
    }

    [Fact]
    public void Update_applies_every_field_including_publication_state()
    {
        var project = NewProject();

        var result = project.Update(
            "Difference Engine",
            "Difference-Engine",
            "Tabulating polynomials.",
            "A long-form description.",
            "https://example.com/project",
            "https://github.com/example/project",
            3,
            isPublished: true,
            new DateOnly(1842, 1, 1),
            new DateOnly(1843, 1, 1),
            Now.AddMinutes(2));

        result.IsSuccess.ShouldBeTrue();
        project.Name.ShouldBe("Difference Engine");
        project.Slug.ShouldBe("difference-engine");
        project.Summary.ShouldBe("Tabulating polynomials.");
        project.Description.ShouldBe("A long-form description.");
        project.Url.ShouldBe("https://example.com/project");
        project.RepositoryUrl.ShouldBe("https://github.com/example/project");
        project.Position.ShouldBe(3);
        project.IsPublished.ShouldBeTrue();
        project.StartedOn.ShouldBe(new DateOnly(1842, 1, 1));
        project.CompletedOn.ShouldBe(new DateOnly(1843, 1, 1));
        project.UpdatedAtUtc.ShouldBe(Now.AddMinutes(2));
    }

    [Fact]
    public void Update_is_atomic_and_leaves_the_project_untouched_when_a_field_is_invalid()
    {
        var project = NewProject();
        project.Update(
            "Difference Engine",
            "difference-engine",
            "Original summary",
            null,
            null,
            null,
            null,
            isPublished: false,
            null,
            null,
            Now);

        var result = project.Update(
            "Changed Name",
            "changed-name",
            "Changed summary",
            "Changed description",
            null,
            "javascript:alert(1)",
            null,
            isPublished: true,
            null,
            null,
            Now.AddHours(1));

        result.Error.ShouldBe(ContentErrors.UrlInvalid);
        project.Name.ShouldBe("Difference Engine");
        project.Slug.ShouldBe("difference-engine");
        project.Summary.ShouldBe("Original summary");
        project.IsPublished.ShouldBeFalse();
        project.UpdatedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void Update_keeps_position_zero_distinct_from_no_position()
    {
        var project = NewProject();

        project.Update("P", "p", null, null, null, null, 0, false, null, null, Now);
        project.Position.ShouldBe(0);

        project.Update("P", "p", null, null, null, null, null, false, null, null, Now);
        project.Position.ShouldBeNull();
    }

    [Fact]
    public void Update_rejects_a_completion_date_before_the_start_date()
    {
        var project = NewProject();

        var result = project.Update(
            "P",
            "p",
            null,
            null,
            null,
            null,
            null,
            false,
            new DateOnly(2020, 6, 1),
            new DateOnly(2020, 1, 1),
            Now);

        result.Error.ShouldBe(ContentErrors.CompletionBeforeStart);
    }

    [Fact]
    public void Update_accepts_matching_start_and_completion_dates()
    {
        var project = NewProject();

        var result = project.Update(
            "P",
            "p",
            null,
            null,
            null,
            null,
            null,
            false,
            new DateOnly(2020, 6, 1),
            new DateOnly(2020, 6, 1),
            Now);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Update_rejects_a_slug_that_would_change_case_only()
    {
        var project = NewProject(slug: "analytical-engine");

        // The slug is normalised on the way in, so an editor cannot create two projects that differ only
        // in the case of their slug by typing the "same" slug with different casing.
        var result = project.Update("P", "Analytical-Engine", null, null, null, null, null, false, null, null, Now);

        result.IsSuccess.ShouldBeTrue();
        project.Slug.ShouldBe("analytical-engine");
    }

    [Fact]
    public void Update_rejects_an_over_long_name()
    {
        var project = NewProject();

        var result = project.Update(new string('x', Project.NameMaxLength + 1), "p", null, null, null, null, null, false, null, null, Now);

        result.Error.ShouldBe(ContentErrors.ProjectNameTooLong);
    }

    [Fact]
    public void Update_rejects_an_over_long_description()
    {
        var project = NewProject();

        var result = project.Update("P", "p", null, new string('x', Project.DescriptionMaxLength + 1), null, null, null, false, null, null, Now);

        result.Error.ShouldBe(ContentErrors.DescriptionTooLong);
    }

    [Fact]
    public void SetPublished_flips_the_flag_and_stamps_the_change()
    {
        var project = NewProject();

        project.SetPublished(true, Now.AddMinutes(9));

        project.IsPublished.ShouldBeTrue();
        project.UpdatedAtUtc.ShouldBe(Now.AddMinutes(9));
    }

    [Fact]
    public void ReplaceTechnologies_replaces_the_previous_set()
    {
        var project = NewProject();
        var original = NewTechnology("C#");
        project.ReplaceTechnologies([original]);

        var replacements = new[] { NewTechnology("F#"), NewTechnology("Rust") };
        var result = project.ReplaceTechnologies(replacements);

        result.IsSuccess.ShouldBeTrue();
        project.Technologies.Count.ShouldBe(2);
        project.Technologies.Select(link => link.Technology.Name).ShouldBe(["F#", "Rust"]);
        project.Technologies.Select(link => link.ProjectId).ShouldAllBe(id => id == project.Id);
    }

    [Fact]
    public void ReplaceTechnologies_rejects_the_same_technology_listed_twice()
    {
        var project = NewProject();
        var technology = NewTechnology("C#");
        project.ReplaceTechnologies([NewTechnology("F#")]);

        var result = project.ReplaceTechnologies([technology, technology]);

        result.Error.ShouldBe(ContentErrors.DuplicateTechnology);
        project.Technologies.Count.ShouldBe(1);
        project.Technologies[0].Technology.Name.ShouldBe("F#");
    }

    [Fact]
    public void ReplaceTechnologies_accepts_an_empty_set()
    {
        var project = NewProject();
        project.ReplaceTechnologies([NewTechnology("C#")]);

        project.ReplaceTechnologies([]);

        project.Technologies.ShouldBeEmpty();
    }

    [Fact]
    public void ReplaceTechnologies_rejects_a_null_entry()
    {
        var project = NewProject();

        Should.Throw<ArgumentException>(() => project.ReplaceTechnologies([null!]));
    }

    [Fact]
    public void NormaliseTechnologyNames_trims_and_removes_case_insensitive_duplicates()
    {
        var result = Project.NormaliseTechnologyNames(["  C#  ", "c#", ".NET", "net"]);

        result.IsSuccess.ShouldBeTrue();
        // The first spelling the editor used survives a case-only duplicate. ".NET" and "net" are kept as
        // separate entries: they are different technologies, not two spellings of one.
        result.Value.ShouldBe(["C#", ".NET", "net"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NormaliseTechnologyNames_rejects_blank_entries(string name)
    {
        var result = Project.NormaliseTechnologyNames(["C#", name]);

        result.Error.ShouldBe(ContentErrors.TechnologyNameRequired);
    }

    [Fact]
    public void NormaliseTechnologyNames_rejects_null()
    {
        var result = Project.NormaliseTechnologyNames(null);

        result.Error.ShouldBe(ContentErrors.TechnologyNameRequired);
    }

    [Fact]
    public void NormaliseTechnologyNames_rejects_an_over_long_name()
    {
        var result = Project.NormaliseTechnologyNames([new string('x', Technology.NameMaxLength + 1)]);

        result.Error.ShouldBe(ContentErrors.TechnologyNameTooLong);
    }

    [Fact]
    public void NormaliseTechnologyNames_accepts_an_empty_list()
    {
        var result = Project.NormaliseTechnologyNames([]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public void Technology_normalises_to_upper_case()
    {
        Technology.Normalise("  .net  ").ShouldBe(".NET");
    }
}