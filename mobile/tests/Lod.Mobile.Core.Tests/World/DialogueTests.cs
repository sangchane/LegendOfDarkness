using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>
/// An NPC's window (0x2F, Hades <c>ServerFormat2F</c>): a kind byte, the NPC, who speaks and what they say, and then
/// whatever that kind carries — choices, goods for sale, the pack's slots, skills to learn. What each kind is answered
/// with is the server's own reading of it (<c>shop1.OnResponse</c>, <c>ClientFormat39</c>).
/// </summary>
public sealed class DialogueTests
{
    private const uint Merchant = 900;

    [Fact]
    public void A_menu_lists_its_choices_with_the_number_each_answers_with()
    {
        Dialogue talk = WorldClient.ReadDialogue(Window(0x00, "무엇을 찾나?",
            [0x02, .. StringA("사기"), 0x00, 0x01, .. StringA("팔기"), 0x00, 0x02]));

        Assert.Equal((Merchant, "상인", "무엇을 찾나?", DialogueKind.Options), (talk.Serial, talk.Who, talk.What, talk.Kind));
        Assert.Equal([new DialogueOption("사기", 1), new DialogueOption("팔기", 2)], talk.Options);
    }

    [Fact]
    public void Words_alone_are_a_menu_with_no_choices()
    {
        Dialogue talk = WorldClient.ReadDialogue(Window(0x00, "잘 가게.", [0x00]));

        Assert.Equal(DialogueKind.Options, talk.Kind);
        Assert.Empty(talk.Options);
    }

    /// <summary>The shop asks "a deal?" about one thing and wants that thing's name back with the answer.</summary>
    [Fact]
    public void A_menu_with_something_to_hand_back_keeps_it()
    {
        Dialogue talk = WorldClient.ReadDialogue(Window(0x01, "도복을 200 에 사겠네.",
            [.. StringA("도복"), 0x01, .. StringA("좋소"), 0x00, 0x19]));

        Assert.Equal(DialogueKind.OptionsWithArgs, talk.Kind);
        Assert.Equal("도복", talk.Args);
        Assert.Equal([new DialogueOption("좋소", 0x19)], talk.Options);
    }

    [Fact]
    public void Goods_for_sale_come_with_picture_colour_price_and_name()
    {
        Dialogue talk = WorldClient.ReadDialogue(Window(0x04, "골라 보게.",
        [
            0x00, 0x04,             // 답할 번호
            0x00, 0x02,             // 두 가지
            0x80, 0x57, 0x00, 0x00, 0x00, 0x05, 0xDC, .. StringA("에페"), .. StringA("Warrior"),
            0x80, 0x01, 0x03, 0x00, 0x01, 0x86, 0xA0, .. StringA("도복"), .. StringA("Monk")
        ]));

        Assert.Equal((DialogueKind.Goods, (ushort)4), (talk.Kind, talk.Step));
        Assert.Equal([new DialogueGoods(0x8057, 0, 1500, "에페"), new DialogueGoods(0x8001, 3, 100000, "도복")], talk.Goods);
    }

    /// <summary>
    /// Selling names slots of the pack. The window carries one byte for the step and the server matches the answer
    /// against that byte shifted up — shop1 waits for 0x0500 after writing 0x05.
    /// </summary>
    [Fact]
    public void Selling_names_slots_of_the_pack_and_is_answered_a_byte_higher()
    {
        Dialogue talk = WorldClient.ReadDialogue(Window(0x05, "무엇을 팔텐가?", [0x05, 0x00, 0x03, 1, 4, 7]));

        Assert.Equal((DialogueKind.PackSlots, (ushort)0x0500), (talk.Kind, talk.Step));
        Assert.Equal([1, 4, 7], talk.Slots);
    }

    [Theory]
    [InlineData(0x06, DialogueKind.Spells)]
    [InlineData(0x07, DialogueKind.Skills)]
    public void Teaching_lists_what_can_be_learned_with_its_icon(byte kind, DialogueKind expected)
    {
        Dialogue talk = WorldClient.ReadDialogue(Window(kind, "무엇을 배우겠나?",
            [0x00, 0x10, 0x00, 0x02, 0x03, 0x00, 0x05, 0x00, .. StringA("단각"), 0x03, 0x00, 0x2A, 0x00, .. StringA("붕각")]));

        Assert.Equal((expected, (ushort)0x10), (talk.Kind, talk.Step));
        Assert.Equal([new DialogueAbility(5, "단각"), new DialogueAbility(42, "붕각")], talk.Abilities);
    }

    [Fact]
    public void Asking_for_words_only_says_which_step_they_answer()
    {
        Dialogue talk = WorldClient.ReadDialogue(Window(0x02, "이름을 말해 보게.", [0x00, 0x07]));

        Assert.Equal((DialogueKind.TextInput, (ushort)7), (talk.Kind, talk.Step));
    }

    /// <summary>
    /// <c>GameClient.CloseDialog</c> sends the raw bytes 0x30 0x00 0x0A 0x00, and the 0x00 after the command is taken
    /// for the ordinal (<c>NetworkPacket</c>) — so what arrives is 0x0A 0x00. A sequence opening starts with its kind,
    /// 0x00 or 0x04.
    /// </summary>
    [Fact]
    public void The_server_shutting_the_window_is_told_apart_from_a_sequence_opening()
    {
        Assert.True(WorldClient.ShutsDialogue([0x0A, 0x00]));
        Assert.False(WorldClient.ShutsDialogue([0x00, 0x01, 0x00, 0x00, 0x03, 0x84]));
        Assert.False(WorldClient.ShutsDialogue([0x04, 0x01, 0x00, 0x00, 0x03, 0x84]));
    }

    private static byte[] Window(byte kind, string what, byte[] data) =>
    [
        kind, 0x01, 0x00, 0x00, 0x03, 0x84, // NPC 900
        0x02, 0x00, 0x01, 0x00, 0x01, 0x02, 0x01, 0x00,
        .. StringB("상인"), .. StringB(what), .. data
    ];

    private static byte[] StringA(string text) => LegacyKoreanEncoding.EncodeStringA(text);

    private static byte[] StringB(string text)
    {
        byte[] bytes = LegacyKoreanEncoding.Encoding.GetBytes(text);
        return [(byte)(bytes.Length >> 8), (byte)bytes.Length, .. bytes];
    }
}
