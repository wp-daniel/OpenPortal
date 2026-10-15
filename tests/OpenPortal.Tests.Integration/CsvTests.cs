using System.Text;
using OpenPortal.Web.Infrastructure;
using Shouldly;

namespace OpenPortal.Tests.Integration;

/// <summary>The CSV reader and writer the user import and the exports share.</summary>
public sealed class CsvTests
{
    [Fact]
    public void Quoted_fields_keep_separators_quotes_and_line_breaks()
    {
        var rows = Csv.Parse("﻿email,name\r\n\"a@x.test\",\"Rossi, \"\"Mario\"\"\nsecond line\"\r\n\r\nb@x.test,Bianchi\n");

        rows.Count.ShouldBe(3);
        rows[0].ShouldBe(["email", "name"]);
        rows[1].ShouldBe(["a@x.test", "Rossi, \"Mario\"\nsecond line"]);
        rows[2].ShouldBe(["b@x.test", "Bianchi"]);
    }

    [Fact]
    public void A_semicolon_header_switches_the_separator()
    {
        var rows = Csv.Parse("email;firstName;lastName\nc@x.test;Anna;Verdi, jr\n");

        rows[1].ShouldBe(["c@x.test", "Anna", "Verdi, jr"]);
    }

    [Fact]
    public void Empty_trailing_fields_are_kept()
    {
        Csv.Parse("a,b,c\n1,,\n")[1].ShouldBe(["1", "", ""]);
    }

    [Fact]
    public void Written_cells_are_quoted_and_defused()
    {
        var csv = new StringBuilder().AppendRow("=SUM(A1)", "a,b", "say \"hi\"", null);

        csv.ToString().TrimEnd().ShouldBe("'=SUM(A1),\"a,b\",\"say \"\"hi\"\"\",");
        Csv.Parse(csv.ToString())[0].ShouldBe(["'=SUM(A1)", "a,b", "say \"hi\"", ""]);
    }
}
