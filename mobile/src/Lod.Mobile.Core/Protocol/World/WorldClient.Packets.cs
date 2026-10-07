using System.Buffers.Binary;
using System.Collections.Concurrent;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.Login;
using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.Protocol.World;

/// <summary>월드 연결 — 받은 패킷 본문을 읽는 정적 함수들.</summary>
public sealed partial class WorldClient
{
    /// <summary>A turn (0x11): whose serial, then which way they now face.</summary>
    public static (uint Serial, Direction Facing) ReadTurn(ReadOnlySpan<byte> body) =>
        (BinaryPrimitives.ReadUInt32BigEndian(body), FromServer(body[4]));

    /// <summary>
    /// A flash (0x29). The server writes the one it lands on first, then whoever made it, then an animation for
    /// each in that order — or, when the first serial is zero, one animation and a tile.
    /// </summary>
    public static Effect ReadEffect(ReadOnlySpan<byte> body)
    {
        uint target = BinaryPrimitives.ReadUInt32BigEndian(body);

        if (target == 0)
        {
            return new Effect(0, 0, BinaryPrimitives.ReadUInt16BigEndian(body[4..]), 0, body[7],
                new Tile(BinaryPrimitives.ReadUInt16BigEndian(body[8..]), BinaryPrimitives.ReadUInt16BigEndian(body[10..])));
        }

        return new Effect(
            target,
            BinaryPrimitives.ReadUInt32BigEndian(body[4..]),
            BinaryPrimitives.ReadUInt16BigEndian(body[8..]),
            BinaryPrimitives.ReadUInt16BigEndian(body[10..]),
            body.Length >= 14 ? BinaryPrimitives.ReadUInt16BigEndian(body[12..]) : 100,
            null);
    }

    /// <summary>A body motion (0x1A): serial, motion number, speed. A short one still names who moved.</summary>
    public static Motion ReadMotion(ReadOnlySpan<byte> body) => new(
        BinaryPrimitives.ReadUInt32BigEndian(body),
        body.Length >= 5 ? body[4] : 0,
        body.Length >= 7 ? BinaryPrimitives.ReadUInt16BigEndian(body[5..]) : 0);

    /// <summary>
    /// 월드맵 창(ServerFormat2E): 그림 이름, 곳의 수, 마당 번호, 그리고 곳마다 점(Y 가 먼저다)·이름·
    /// 갈 맵·그 맵에서 설 칸. 끝의 여섯 바이트는 서버가 채우는 아무 값이라 읽지 않는다.
    /// </summary>
    public static WorldMapInfo ReadWorldMap(ReadOnlySpan<byte> body)
    {
        int at = 0;

        string picture = Words(body, ref at);
        int count = Byte(body, ref at);
        int number = Byte(body, ref at);

        List<WorldMapNode> nodes = new(count);

        for (int index = 0; index < count; index++)
        {
            try
            {
                int pointY = (short)Word(body, ref at);
                int pointX = (short)Word(body, ref at);
                string name = Words(body, ref at);
                int area = (int)Long(body, ref at);
                int x = (short)Word(body, ref at);
                int y = (short)Word(body, ref at);

                nodes.Add(new WorldMapNode(name, area, x, y, pointX, pointY));
            }
            catch (ProtocolException) when (nodes.Count > 0)
            {
                // 개수(0x02 등)와 몸통이 어긋나 중간에서 끊겨도, 이미 다 읽은 곳이 있으면 그것으로
                // 돌려준다 — 사람이 창을 보고 빠져나갈 수 있다. 하나도 못 읽었으면(맨 처음이 끊기면)
                // 아래에서 그대로 던진다 — 그건 어차피 못 빠져나온다.
                break;
            }
        }

        return new WorldMapInfo(picture, number, nodes);
    }

    /// <summary>A sound (0x19): an empty byte, then the number.</summary>
    public static int ReadSound(ReadOnlySpan<byte> body) => BinaryPrimitives.ReadUInt16BigEndian(body[1..]);

    /// <summary>
    /// The sound a health bar (0x13) carries in its last byte — a blow lands with it, and a sound meant for
    /// nobody's bar comes this way too, with the bar left at 255. Zero is what a template with no sound
    /// writes, and 255 is the original's "none"; neither is played.
    /// </summary>
    public static int? ReadHealthSound(ReadOnlySpan<byte> body) =>
        body.Length >= 7 && body[6] is not (0 or byte.MaxValue) ? body[6] : null;

    /// <summary>Somebody to draw: where they are, which way they face, what they wear, and their name.</summary>
    /// <remarks>
    /// Place, direction and serial take nine bytes; then twenty-one bytes of wardrobe, one byte saying
    /// whether the map allows killing, and the name. A dead character is written bare and the server stops
    /// before the name, which is why the name is only read when there are bytes left for it.
    /// </remarks>
    public static Character ReadCharacter(ReadOnlySpan<byte> body)
    {
        const int fixedLength = 31;
        const int wornLength = 11;

        if (body.Length < wornLength)
        {
            throw new ProtocolException($"사람 안내가 {wornLength}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        uint serial = BinaryPrimitives.ReadUInt32BigEndian(body[5..]);
        Tile where = new(BinaryPrimitives.ReadUInt16BigEndian(body), BinaryPrimitives.ReadUInt16BigEndian(body[2..]));
        Direction facing = FromServer(body[4]);
        int head = BinaryPrimitives.ReadUInt16BigEndian(body[9..]);

        // Somebody wearing a monster's shape carries its number where the wardrobe would be, and the rest
        // of the packet is laid out differently.
        // ponytail: read the name and leave the shape alone — no map here has monsters to check it against.
        if (head == 0xFFFF)
        {
            return new Character(serial, where, facing, null, ReadName(body, 22));
        }

        if (body.Length < fixedLength)
        {
            throw new ProtocolException($"사람 안내가 {fixedLength}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        // Byte 15 is the armour written a second time and byte 26 is left empty; both are the server's habit.
        Appearance wearing = new(
            head,
            body[11],
            BinaryPrimitives.ReadUInt16BigEndian(body[12..]),
            body[14],
            body[17],
            body[18],
            body[19],
            body[20],
            BinaryPrimitives.ReadUInt16BigEndian(body[21..]),
            body[23],
            BinaryPrimitives.ReadUInt16BigEndian(body[24..]),
            body[27],
            BinaryPrimitives.ReadUInt16BigEndian(body[28..]));

        return new Character(serial, where, facing, wearing, ReadName(body, fixedLength));
    }

    /// <summary>
    /// Our own numbers. The server sends them in four pieces and a leading flag byte says which of them
    /// came, so a piece left out of this packet keeps whatever it said last time — which is what
    /// <paramref name="before" /> is for. It sends all four as the character enters and single pieces
    /// afterwards: what is left of health and mana every time anything is struck, for instance.
    /// </summary>
    /// <remarks>
    /// The pieces are fixed width and always in this order: the standing figures (28 bytes), what is left
    /// of health and mana (8), what has been earned (24), and the fighting figures (13). Two flags the
    /// server always sets, 0x40 and 0x80, say nothing about the body and are passed over.
    /// </remarks>
    public static Vitals ReadVitals(ReadOnlySpan<byte> body, Vitals? before = null)
    {
        const byte Standing = 0x20;
        const byte Remaining = 0x10;
        const byte Earned = 0x08;
        const byte Fighting = 0x04;

        if (body.Length < 1)
        {
            throw new ProtocolException("몸 상태 안내에 조각 표가 없습니다 (0바이트).");
        }

        byte pieces = body[0];
        int at = 1;

        // Static because a local function may not reach a span, and the span is the one thing it does not
        // need: the length is enough to say the piece is not all there.
        static void Require(int have, int wanted, byte pieces)
        {
            if (have < wanted)
            {
                throw new ProtocolException(
                    $"몸 상태 안내가 {wanted}바이트보다 짧습니다 ({have}바이트, 조각 표 0x{pieces:X2}).");
            }
        }

        Vitals now = before ?? Vitals.Unknown;

        if ((pieces & Standing) != 0)
        {
            Require(body.Length, at + 28, pieces);

            now = now with
            {
                // Three bytes the server always writes as 1, 0, 0 come first.
                Level = body[at + 3],
                AbilityLevel = body[at + 4],
                MaximumHealth = (int)BinaryPrimitives.ReadUInt32BigEndian(body[(at + 5)..]),
                MaximumMana = (int)BinaryPrimitives.ReadUInt32BigEndian(body[(at + 9)..]),
                Str = body[at + 13],
                Int = body[at + 14],
                Wis = body[at + 15],
                Con = body[at + 16],
                Dex = body[at + 17],

                // A flag saying there is something to spend, then how much.
                Unspent = body[at + 18] == 0 ? 0 : body[at + 19],
                MaximumWeight = BinaryPrimitives.ReadUInt16BigEndian(body[(at + 20)..]),
                Weight = BinaryPrimitives.ReadUInt16BigEndian(body[(at + 22)..]),
            };

            at += 28;
        }

        if ((pieces & Remaining) != 0)
        {
            Require(body.Length, at + 8, pieces);

            now = now with
            {
                Health = (int)BinaryPrimitives.ReadUInt32BigEndian(body[at..]),
                Mana = (int)BinaryPrimitives.ReadUInt32BigEndian(body[(at + 4)..]),
            };

            at += 8;
        }

        if ((pieces & Earned) != 0)
        {
            Require(body.Length, at + 24, pieces);

            now = now with
            {
                Experience = BinaryPrimitives.ReadUInt32BigEndian(body[at..]),
                ExperienceToGo = BinaryPrimitives.ReadUInt32BigEndian(body[(at + 4)..]),
                AbilityExperience = BinaryPrimitives.ReadUInt32BigEndian(body[(at + 8)..]),
                AbilityExperienceToGo = BinaryPrimitives.ReadUInt32BigEndian(body[(at + 12)..]),
                GamePoints = BinaryPrimitives.ReadUInt32BigEndian(body[(at + 16)..]),
                Gold = BinaryPrimitives.ReadUInt32BigEndian(body[(at + 20)..]),
            };

            at += 24;
        }

        if ((pieces & Fighting) != 0)
        {
            Require(body.Length, at + 13, pieces);

            now = now with
            {
                // Four bytes of nothing, then blindness, then another byte of nothing.
                Blind = body[at + 4] != 0,
                Offense = (Element)body[at + 6],
                Defense = (Element)body[at + 7],
                MagicResistance = body[at + 8],

                // Signed, because armour worth having is below zero.
                Armor = (sbyte)body[at + 10],
                Damage = body[at + 11],
                Hit = body[at + 12],
            };
        }

        return now;
    }

    /// <summary>
    /// One status icon (0x3A): which picture, and a grade saying how much longer it lasts. The server works the
    /// grade out in <c>Debuff.Display</c> — 6 is over ninety seconds, 1 is under ten, and 0 means it is over.
    /// </summary>
    public static Ailment ReadAilment(ReadOnlySpan<byte> body)
    {
        if (body.Length < 3)
        {
            throw new ProtocolException($"상태 안내가 3바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new Ailment(BinaryPrimitives.ReadUInt16BigEndian(body), body[2]);
    }

    /// <summary>
    /// Something on somebody else (0x5C, our server's own packet): serial(4) · picture(2) · grade(1) · harmful(1) ·
    /// effect(2), all big-endian like the rest.
    /// </summary>
    public static SeenAilment ReadSeenAilment(ReadOnlySpan<byte> body)
    {
        if (body.Length < 10)
        {
            throw new ProtocolException($"남의 상태 안내가 10바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new SeenAilment(
            BinaryPrimitives.ReadUInt32BigEndian(body),
            BinaryPrimitives.ReadUInt16BigEndian(body[4..]),
            body[6],
            body[7] != 0,
            BinaryPrimitives.ReadUInt16BigEndian(body[8..]));
    }

    /// <summary>
    /// How much a blow took or a heal gave (0x5D, our server's own packet): target(4) · source(4) · amount(4) ·
    /// kind(1, 0 damage · 1 heal), all big-endian like the rest. An unknown kind is read as damage.
    /// </summary>
    public static Figure ReadFigure(ReadOnlySpan<byte> body)
    {
        if (body.Length < 13)
        {
            throw new ProtocolException($"피해·회복 안내가 13바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new Figure(
            BinaryPrimitives.ReadUInt32BigEndian(body),
            BinaryPrimitives.ReadUInt32BigEndian(body[4..]),
            (int)Math.Min(int.MaxValue, BinaryPrimitives.ReadUInt32BigEndian(body[8..])),
            body[12] == 1 ? FigureKind.Heal : FigureKind.Damage);
    }

    /// <summary>
    /// Everything the server is showing at once: how many, then that many records.
    /// </summary>
    /// <remarks>
    /// Each record is the same seventeen bytes — place, serial, drawing, four bytes for how many a thing on the floor
    /// holds (<see cref="Creature.Count" />, empty for the rest), a direction, one more empty, and what kind of thing it
    /// is. A merchant is named after that and nothing else is. Things lying on the floor come in the same seventeen
    /// bytes, as <see cref="CreatureKind.Passable" />.
    /// </remarks>
    public static IReadOnlyList<Creature> ReadCreatures(ReadOnlySpan<byte> body)
    {
        const int recordLength = 17;

        if (body.Length < 2)
        {
            throw new ProtocolException($"물체 안내에 개수가 없습니다 ({body.Length}바이트).");
        }

        int expected = BinaryPrimitives.ReadUInt16BigEndian(body);
        List<Creature> shown = [];
        ReadOnlySpan<byte> rest = body[2..];

        for (int index = 0; index < expected; index++)
        {
            if (rest.Length < recordLength)
            {
                throw new ProtocolException(
                    $"물체 {index + 1}번째가 {recordLength}바이트보다 짧습니다 ({rest.Length}바이트).");
            }

            CreatureKind kind = (CreatureKind)rest[16];
            string name = string.Empty;
            int read = recordLength;

            if (kind == CreatureKind.Merchant)
            {
                name = LegacyKoreanEncoding.DecodeStringA(rest[recordLength..], out int consumed);
                read += consumed;
            }

            shown.Add(new Creature(
                BinaryPrimitives.ReadUInt32BigEndian(rest[4..]),
                new Tile(BinaryPrimitives.ReadUInt16BigEndian(rest), BinaryPrimitives.ReadUInt16BigEndian(rest[2..])),
                FromServer(rest[14]),
                BinaryPrimitives.ReadUInt16BigEndian(rest[8..]),
                kind,
                name,
                (int)Math.Min(ushort.MaxValue, BinaryPrimitives.ReadUInt32BigEndian(rest[10..]))));

            rest = rest[read..];
        }

        return shown;
    }

    /// <summary>
    /// Something the server has put in our pack: which slot, what it looks like, what it is called, how
    /// many, and how worn out.
    /// </summary>
    public static InventoryItem ReadPackItem(ReadOnlySpan<byte> body)
    {
        const int beforeName = 4;

        if (body.Length < beforeName + 1)
        {
            throw new ProtocolException($"소지품 안내가 {beforeName + 1}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        string name = LegacyKoreanEncoding.DecodeStringA(body[beforeName..], out int consumed);
        ReadOnlySpan<byte> rest = body[(beforeName + consumed)..];

        if (rest.Length < 13)
        {
            throw new ProtocolException($"소지품 안내의 이름 뒤가 13바이트보다 짧습니다 ({rest.Length}바이트).");
        }

        return new InventoryItem(
            body[0],
            BinaryPrimitives.ReadUInt16BigEndian(body[1..]),
            body[3],
            name,
            (int)BinaryPrimitives.ReadUInt32BigEndian(rest),
            // Byte 4 says whether it stacks, which the count already tells us.
            (int)BinaryPrimitives.ReadUInt32BigEndian(rest[9..]),
            (int)BinaryPrimitives.ReadUInt32BigEndian(rest[5..]),
            // 13~16바이트로 끝나면 수치가 없는 것이다 — 자르다 넘치지 않게.
            ReadItemStats(rest[Math.Min(17, rest.Length)..]));
    }

    /// <summary>
    /// Our server's addition after the original's end of 0x0F and 0x37: a 1, then the item's numbers
    /// (ServerFormat0F.WriteNumbers). An original server ends there, so there is nothing to show.
    /// </summary>
    /// <summary>One item's numbers with what it restores — the shop list writes them back to back.</summary>
    internal const int ItemNumbersSize = 1 + (9 * 2) + (4 * 4) + 6 + 4 + 1 + 8;

    internal static ItemStats? ReadItemStats(ReadOnlySpan<byte> tail)
    {
        const int size = 1 + (9 * 2) + (4 * 4) + 6 + 4 + 1;

        if (tail.Length < size || tail[0] != 1)
        {
            return null;
        }

        ReadOnlySpan<byte> s = tail[1..];
        ReadOnlySpan<byte> wide = s[18..];
        ReadOnlySpan<byte> small = wide[16..];
        // 체력·마력 회복(물약)은 2026-10-05 에 덧붙였다 — 그 전 서버는 없다.
        ReadOnlySpan<byte> restore = small[11..];
        bool restores = restore.Length >= 8;

        return new ItemStats(
            Short(s, 0), Short(s, 1), Short(s, 2), Short(s, 3), Short(s, 4), Short(s, 5), Short(s, 6), Short(s, 7), Short(s, 8),
            Int(wide, 0), Int(wide, 1), Int(wide, 2), Int(wide, 3),
            small[0], small[1], small[2], small[3], small[4], small[5],
            BinaryPrimitives.ReadUInt32BigEndian(small[6..]),
            small[10],
            restores ? Int(restore, 0) : 0,
            restores ? Int(restore, 1) : 0);
    }

    private static int Short(ReadOnlySpan<byte> from, int nth) => BinaryPrimitives.ReadInt16BigEndian(from[(nth * 2)..]);

    private static int Int(ReadOnlySpan<byte> from, int nth) => BinaryPrimitives.ReadInt32BigEndian(from[(nth * 4)..]);

    /// <summary>
    /// One piece of gear the character is wearing. The place it sits comes first, then the picture, then a
    /// byte the server always writes as three, then two names — what the item is and what this one is
    /// called — and how worn out it is.
    /// </summary>
    public static WornItem ReadWorn(ReadOnlySpan<byte> body)
    {
        const int beforeName = 4;

        if (body.Length < beforeName + 1)
        {
            throw new ProtocolException($"장비 안내가 {beforeName + 1}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        string name = LegacyKoreanEncoding.DecodeStringA(body[beforeName..], out int consumed);
        ReadOnlySpan<byte> rest = body[(beforeName + consumed)..];
        string called = LegacyKoreanEncoding.DecodeStringA(rest, out int alsoConsumed);
        ReadOnlySpan<byte> wear = rest[alsoConsumed..];

        if (wear.Length < 8)
        {
            throw new ProtocolException($"장비 안내의 이름 뒤가 8바이트보다 짧습니다 ({wear.Length}바이트).");
        }

        return new WornItem(
            body[0],
            BinaryPrimitives.ReadUInt16BigEndian(body[1..]),
            name,
            called,
            BinaryPrimitives.ReadUInt32BigEndian(wear),
            BinaryPrimitives.ReadUInt32BigEndian(wear[4..]),
            ReadItemStats(wear[8..]));
    }

    /// <summary>Reads how long one slot must wait (0x3F).</summary>
    public static Cooldown ReadCooldown(ReadOnlySpan<byte> body)
    {
        const int wanted = 6;

        if (body.Length < wanted)
        {
            throw new ProtocolException($"다시 쓰기까지의 안내가 {wanted}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new Cooldown(body[0] == 1, body[1], (int)BinaryPrimitives.ReadUInt32BigEndian(body[2..]));
    }

    /// <summary>
    /// Reads one line the server says (0x0A): the type byte, then the words as a two-byte-length string
    /// (Hades <c>ServerFormat0A.Serialize</c>). A packet with no words — Hades leaves the string out when it is empty —
    /// is nothing to show.
    /// </summary>
    public static (byte Type, string Text)? ReadTold(ReadOnlySpan<byte> body) =>
        body.Length > 3 ? (body[0], LegacyKoreanEncoding.DecodeStringB(body[1..], out _)) : null;

    /// <summary>Reads one line of speech (0x0D): how it was said, whose it is, and the words.</summary>
    public static Spoken ReadSpoken(ReadOnlySpan<byte> body)
    {
        const int beforeText = 5;

        if (body.Length < beforeText + 1)
        {
            throw new ProtocolException($"누가 한 말이 {beforeText + 1}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new Spoken(
            (SpeechKind)body[0],
            BinaryPrimitives.ReadUInt32BigEndian(body[1..]),
            LegacyKoreanEncoding.DecodeStringA(body[beforeText..], out _));
    }

    /// <summary>A skill pane row: slot, icon, then its display name as a short string.</summary>
    public static LearnedSkill ReadSkill(ReadOnlySpan<byte> body)
    {
        const int beforeName = 3;

        if (body.Length < beforeName + 1)
        {
            throw new ProtocolException($"기술 안내가 {beforeName + 1}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new LearnedSkill(
            body[0],
            BinaryPrimitives.ReadUInt16BigEndian(body[1..]),
            LegacyKoreanEncoding.DecodeStringA(body[beforeName..], out _));
    }

    /// <summary>A spell pane row, including how it obtains a target and how many chant lines it uses.</summary>
    public static LearnedSpell ReadSpell(ReadOnlySpan<byte> body)
    {
        const int beforeName = 4;

        if (body.Length < beforeName + 1)
        {
            throw new ProtocolException($"마법 안내가 {beforeName + 1}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        string name = LegacyKoreanEncoding.DecodeStringA(body[beforeName..], out int nameBytes);
        ReadOnlySpan<byte> afterName = body[(beforeName + nameBytes)..];

        if (afterName.Length < 1)
        {
            throw new ProtocolException("마법 안내에 설명이 없습니다.");
        }

        string prompt = LegacyKoreanEncoding.DecodeStringA(afterName, out int promptBytes);
        ReadOnlySpan<byte> afterPrompt = afterName[promptBytes..];

        if (afterPrompt.Length < 1)
        {
            throw new ProtocolException("마법 안내에 시전 줄 수가 없습니다.");
        }

        return new LearnedSpell(
            body[0],
            BinaryPrimitives.ReadUInt16BigEndian(body[1..]),
            (SpellTargetType)body[3],
            name,
            prompt,
            afterPrompt[0]);
    }

    /// <summary>The server removes a learned skill or spell by pane slot alone.</summary>
    public static int ReadAbilitySlot(ReadOnlySpan<byte> body)
    {
        if (body.Length < 1)
        {
            throw new ProtocolException("기술·마법 칸 삭제 안내가 비어 있습니다.");
        }

        return body[0];
    }

    /// <summary>
    /// What an NPC answered when tapped. The head is fixed — a kind byte, the NPC's serial and picture,
    /// and five bytes the original client reads and ignores — and then two strings: who is speaking and
    /// what they said. Both are length-prefixed the long way round (a big-endian ushort), unlike almost
    /// everything else in this protocol, which is why this reads them with DecodeStringB.
    /// </summary>
    public static Dialogue ReadDialogue(ReadOnlySpan<byte> body)
    {
        const int beforeName = 14;

        if (body.Length <= beforeName)
        {
            return new Dialogue(0, string.Empty, string.Empty);
        }

        uint serial = (uint)((body[2] << 24) | (body[3] << 16) | (body[4] << 8) | body[5]);
        string who = LegacyKoreanEncoding.DecodeStringB(body[beforeName..], out int consumed);
        ReadOnlySpan<byte> rest = body[(beforeName + consumed)..];
        string what = rest.Length > 0 ? LegacyKoreanEncoding.DecodeStringB(rest, out consumed) : string.Empty;

        Dialogue talk = new(serial, who, what) { Kind = (DialogueKind)body[0] };

        try
        {
            return rest.Length > 0 ? WithWindowData(talk, rest[consumed..]) : talk;
        }
        catch (ProtocolException cut)
        {
            // The words are still worth showing when what follows them is cut short — but the window says so.
            return talk with { Unread = cut.Message };
        }
    }

    /// <summary>
    /// Reads what one kind of window carries under its words, the way each Hades <c>IDialogData</c> writes it.
    /// </summary>
    private static Dialogue WithWindowData(Dialogue talk, ReadOnlySpan<byte> data)
    {
        int at = 0;

        switch (talk.Kind)
        {
            case DialogueKind.Options or DialogueKind.OptionsWithArgs:
            {
                string args = talk.Kind == DialogueKind.OptionsWithArgs ? Words(data, ref at) : string.Empty;
                int count = Byte(data, ref at);
                List<DialogueOption> options = [];

                // OptionsData counts a choice with no words but writes nothing for it, so the bytes decide.
                for (int i = 0; i < count && at < data.Length; i++)
                {
                    options.Add(new DialogueOption(Words(data, ref at), Word(data, ref at)));
                }

                return talk with { Args = args, Options = options };
            }

            case DialogueKind.TextInput or DialogueKind.ForgetSpell or DialogueKind.ForgetSkill:
                return talk with { Step = Word(data, ref at) };

            case DialogueKind.Goods:
            {
                ushort step = Word(data, ref at);
                int count = Word(data, ref at);
                List<DialogueGoods> goods = [];

                for (int i = 0; i < count; i++)
                {
                    goods.Add(new DialogueGoods(
                        Icon: Word(data, ref at),
                        Colour: Byte(data, ref at),
                        Price: Long(data, ref at),
                        Name: Words(data, ref at),
                        Class: Words(data, ref at),
                        Gender: at < data.Length ? Byte(data, ref at) : (byte)255,
                        Circle: at < data.Length ? Byte(data, ref at) : (byte)0));

                    // 상점 패킷의 직업 문자열은 이제 모바일 필터에 사용한다.
                }

                // 우리 확장(2026-10-05): 목록 뒤에 물건마다 수치(ServerFormat0F.WriteNumbers, 회복량까지) — 옛 서버는 없다.
                for (int i = 0; i < goods.Count && data.Length - at >= ItemNumbersSize; i++)
                {
                    goods[i] = goods[i] with { Stats = ReadItemStats(data.Slice(at, ItemNumbersSize)) };
                    at += ItemNumbersSize;
                }

                return talk with { Step = step, Goods = goods };
            }

            case DialogueKind.PackSlots:
            {
                // ItemSellData 는 번호를 한 바이트만 쓰고, shop1 은 그 바이트를 한 자리 올린 값(0x05 → 0x0500)을 기다린다.
                ushort step = (ushort)(Byte(data, ref at) << 8);
                int count = Word(data, ref at);
                List<int> slots = [];

                for (int i = 0; i < count; i++)
                {
                    slots.Add(Byte(data, ref at));
                }

                return talk with { Step = step, Slots = slots };
            }

            case DialogueKind.Spells or DialogueKind.Skills:
            {
                ushort step = Word(data, ref at);
                int count = Word(data, ref at);
                List<DialogueAbility> abilities = [];

                for (int i = 0; i < count; i++)
                {
                    Byte(data, ref at); // 마법 2 · 기술 3
                    int icon = Word(data, ref at);
                    Byte(data, ref at);
                    abilities.Add(new DialogueAbility(icon, Words(data, ref at)));
                }

                return talk with { Step = step, Abilities = abilities };
            }

            default:
                return talk;
        }
    }

    /// <summary>
    /// <c>GameClient.CloseDialog</c> sends the raw bytes 0x30 0x00 0x0A 0x00; the 0x00 after the command goes for the
    /// ordinal, so the body starts 0x0A. A sequence opening starts with its kind instead (0x00, or 0x04 for typing).
    /// </summary>
    public static bool ShutsDialogue(ReadOnlySpan<byte> body) => body.Length >= 1 && body[0] == 0x0A;

    private static byte Byte(ReadOnlySpan<byte> data, ref int at) =>
        at < data.Length ? data[at++] : throw new ProtocolException("대화창 자료가 중간에 끊겼습니다.");

    private static ushort Word(ReadOnlySpan<byte> data, ref int at) => (ushort)((Byte(data, ref at) << 8) | Byte(data, ref at));

    private static uint Long(ReadOnlySpan<byte> data, ref int at) => ((uint)Word(data, ref at) << 16) | Word(data, ref at);

    private static string Words(ReadOnlySpan<byte> data, ref int at)
    {
        string words = LegacyKoreanEncoding.DecodeStringA(data[Math.Min(at, data.Length)..], out int consumed);
        at += consumed;

        return words;
    }

    /// <summary>The name, if the server got as far as writing one.</summary>
    private static string ReadName(ReadOnlySpan<byte> body, int at) =>
        body.Length > at ? LegacyKoreanEncoding.DecodeStringA(body[at..], out _) : string.Empty;

    private static Direction FromServer(byte direction) => direction switch
    {
        0 => Direction.North,
        1 => Direction.East,
        2 => Direction.South,
        _ => Direction.West
    };

    /// <summary>Map number, its size in tiles, flags, a spare word, a hash of the file, then its name.</summary>
    private static MapInfo ReadMap(ReadOnlySpan<byte> body)
    {
        const int fixedLength = 9;

        if (body.Length < fixedLength)
        {
            throw new ProtocolException($"지도 안내가 {fixedLength}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new MapInfo(
            BinaryPrimitives.ReadUInt16BigEndian(body),
            body[2],
            body[3],
            LegacyKoreanEncoding.DecodeStringA(body[fixedLength..], out _));
    }

    /// <summary>The character's tile, then the size of the view around it, which we do not use yet.</summary>
    private static Tile ReadLocation(ReadOnlySpan<byte> body)
    {
        if (body.Length < 4)
        {
            throw new ProtocolException($"위치 안내가 4바이트보다 짧습니다 ({body.Length}바이트).");
        }

        return new Tile(
            BinaryPrimitives.ReadInt16BigEndian(body),
            BinaryPrimitives.ReadInt16BigEndian(body[2..]));
    }
}
