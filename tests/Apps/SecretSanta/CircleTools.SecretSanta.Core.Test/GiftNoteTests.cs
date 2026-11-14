// SPDX-FileCopyrightText: 2026 CircleTools contributors
// SPDX-License-Identifier: Apache-2.0

using CircleTools.SecretSanta.Core.Domain;

namespace CircleTools.SecretSanta.Core.Test;

/// <summary>
/// Domain tests for a member's stable gift note (FR-037): set, trim, clear, the length cap that bounds
/// the encoded reveal link (ADR-0014), and that it survives a rename (independent operations).
/// </summary>
public sealed class GiftNoteTests
{
    [Fact]
    public void SetMemberGiftNote_TrimsAndStores()
    {
        var group = Group.Create("Family");
        var alice = group.AddMember("Alice");

        group.SetMemberGiftNote(alice, "  No nuts — severe allergy  ");

        Assert.Equal("No nuts — severe allergy", group.FindMember(alice)!.GiftNote);
    }

    [Fact]
    public void SetMemberGiftNote_EmptyOrWhitespace_Clears()
    {
        var group = Group.Create("Family");
        var alice = group.AddMember("Alice");
        group.SetMemberGiftNote(alice, "Vegetarian");

        group.SetMemberGiftNote(alice, "   ");

        Assert.Equal(string.Empty, group.FindMember(alice)!.GiftNote);
    }

    [Fact]
    public void SetMemberGiftNote_OverTheCap_Throws()
    {
        var group = Group.Create("Family");
        var alice = group.AddMember("Alice");

        var tooLong = new string('x', Member.MaxGiftNoteLength + 1);

        Assert.Throws<ArgumentException>(() => group.SetMemberGiftNote(alice, tooLong));
    }

    [Fact]
    public void SetMemberGiftNote_AtTheCap_IsAccepted()
    {
        var group = Group.Create("Family");
        var alice = group.AddMember("Alice");

        var atCap = new string('x', Member.MaxGiftNoteLength);
        group.SetMemberGiftNote(alice, atCap);

        Assert.Equal(atCap, group.FindMember(alice)!.GiftNote);
    }

    [Fact]
    public void RenamingAMember_DoesNotClobberTheGiftNote()
    {
        var group = Group.Create("Family");
        var alice = group.AddMember("Alice");
        group.SetMemberGiftNote(alice, "No nuts");

        group.EditMember(alice, "Alicia");

        Assert.Equal("Alicia", group.FindMember(alice)!.Name);
        Assert.Equal("No nuts", group.FindMember(alice)!.GiftNote);
    }

    [Fact]
    public void GiftNote_RoundTripsThroughState()
    {
        var group = Group.Create("Family");
        var alice = group.AddMember("Alice");
        group.SetMemberGiftNote(alice, "No nuts");

        var restored = Group.FromState(group.ToState());

        Assert.Equal("No nuts", restored.FindMember(alice)!.GiftNote);
    }
}
