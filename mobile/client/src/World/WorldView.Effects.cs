using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;

namespace LodClient;

/// <summary>월드 화면 — 이펙트·말풍선·피해 숫자·소리·휘두르기.</summary>
public sealed partial class WorldView
{
    /// <summary>
    /// Draws the flashes the server asked for (0x29). On somebody the first picture goes over whoever it lands
    /// on and the second over whoever made it; on the ground it goes on the tile.
    /// </summary>
    private void Flashes()
    {
        while (server is { } world && world.TakeEffect(out Effect? effect))
        {
            if (effect.At is Tile at)
            {
                Show(effect.TargetAnimation, Ground(at), effect.Speed);
                continue;
            }

            if (effect.TargetAnimation > 0 && Someone(world, effect.Target) is { } target)
            {
                Show(effect.TargetAnimation, target.Position, effect.Speed, target);
            }

            if (effect.SourceAnimation > 0 && Someone(world, effect.Source) is { } source)
            {
                Show(effect.SourceAnimation, source.Position, effect.Speed, source);
            }
        }
    }

    // 무엇이 없어서 안 보였는지 한 번씩만 적는다. 같은 번호로 계속 적으면 기록이 그것만 남는다(2026-09-18 조사).
    private readonly HashSet<string> _toldAbout = [];

    private void Told(string what)
    {
        if (_toldAbout.Add(what))
        {
            GD.Print($"GREYBOX_MISSING {what}");
        }
    }

    private void Show(int number, Vector2 feet, int speed, Actor? on = null)
    {
        if (number <= 0)
        {
            return;
        }

        if (Flash.Make(number, speed) is not { } flash)
        {
            Told($"이펙트 {number} 그림이 없습니다");
            return;
        }

        // 혼수인 동안은 혼수가 머리 칸을 차지한다 — 다른 머리 이펙트(Miss·일음지 …)는 그리지 않는다.
        if (flash.OnHead && on is { Comatose: true } && number != Overhead.ComaEffect)
        {
            flash.Free();
            return;
        }

        // 그림이 제 기준점을 지니므로 발밑(칸)에 놓는다 — 몸 가운데는 그림이 정한다. 머리 이펙트만은 맞은
        // 이의 그려진 머리 바로 위 칸으로 옮긴다(Overhead.Shift) — 원작 칸 그대로면 키 큰 사람의 얼굴을 덮었다.
        flash.Land(feet, on?.HeadTop);
        _camera.AddChild(flash);

        if (flash.OnHead)
        {
            GD.Print($"GREYBOX_HEAD_FLASH {number} frame {Engine.GetProcessFrames()} on {on?.DisplayName}");
        }
    }

    // 배경음악은 효과음과 따로 한 대에서 돈다 — 맵을 옮기면 갈아 끼우고, 같은 곡이면 이어서 튼다.
    private AudioStreamPlayer? _band;

    private int _playing = -1;

    /// <summary>
    /// Plays the map's music (0x19 with a number of 128 or more). One song at a time, looping, and the same song is
    /// left alone when the next map asks for it again.
    /// </summary>
    private void Band()
    {
        while (server is { } world && world.TakeMusic(out int song))
        {
            if (song == _playing)
            {
                continue;
            }

            if (song == Music.Silence)
            {
                _band?.Stop();
                _playing = -1;
                continue;
            }

            string path = $"res://assets/music/{song}.ogg";

            if (!ResourceLoader.Exists(path))
            {
                Told($"곡 {song} 파일이 없습니다");
                continue;
            }

            if (_band is null)
            {
                _band = new AudioStreamPlayer { Name = "Band" };
                AddChild(_band);
            }

            if (GD.Load<AudioStream>(path) is AudioStreamOggVorbis stream)
            {
                stream.Loop = true;
                _band.Stream = stream;
                _band.Play();
                _playing = song;
            }
        }
    }

    /// <summary>
    /// Puts what somebody said over their head (0x0D) — a person, a monster, or a merchant's sign. Somebody the screen
    /// is not drawing has nowhere to put it; the log still has it.
    /// </summary>
    public void Speak(uint serial, string words)
    {
        if (server is { } world && serial == world.Serial)
        {
            _player.Say(words);
        }
        else if (_crowd.TryGetValue(serial, out Actor? person))
        {
            person.Say(words);
        }
        else if (_herd.TryGetValue(serial, out Actor? beast))
        {
            beast.Say(words);
        }
        else if (_signs.TryGetValue(serial, out NpcMark? sign))
        {
            if (sign.GetNodeOrNull<SpeechBubble>("Speech") is not { } bubble)
            {
                bubble = new SpeechBubble { Name = "Speech", Position = new Vector2(0, -NpcMark.Waist * 2 - 4) };
                sign.AddChild(bubble);
            }

            bubble.Say(words);
        }
    }

    /// <summary>
    /// Floats how much a blow took or a heal gave over whoever it was (0x5D). Read after <see cref="Wounds" /> so a
    /// blow's bar is already up and the number starts above it.
    /// </summary>
    private void Figures()
    {
        while (server is { } world && world.TakeFigure(out Figure? figure))
        {
            Actor? on = figure.Target == world.Serial ? _player
                : _herd.TryGetValue(figure.Target, out Actor? beast) ? beast
                : _crowd.TryGetValue(figure.Target, out Actor? person) ? person
                : null;

            if (on is not null && figure.Amount > 0)
            {
                _figures.Add(on, on.FigureStart, figure, world.Serial);
                GD.Print($"GREYBOX_FIGURE {FloatingFigure.Tone(figure, world.Serial)} {FloatingFigure.Text(figure)} on {on.DisplayName}");
            }
        }
    }

    /// <summary>
    /// Puts a bar over the head of whoever was just struck. The server tells us about every blow (0x13) with
    /// what is left as a percentage, and until now the screen only listened for the sound in it — so a fight
    /// showed no sign of how it was going, on a monster or on a person.
    /// </summary>
    private void Wounds()
    {
        while (server is { } world && world.TakeHurt(out uint serial, out int left))
        {
            // 번호 0 은 허공을 친 것이다 — 아무의 체력도 아니다.
            if (serial == 0)
            {
                continue;
            }

            if (serial == world.Serial)
            {
                _player.Struck(left);
            }
            else if (_herd.TryGetValue(serial, out Actor? beast))
            {
                beast.Struck(left);
            }
            else if (_crowd.TryGetValue(serial, out Actor? person))
            {
                person.Struck(left);
            }
        }

        // 내 것은 0x3A 로(원작 그대로), 남의 것은 우리 서버가 둘레에 알리는 0x5C 로 온다.
        if (server is { } mine)
        {
            _player.Ailing(mine.Ailments);

            // 사람은 체력바 아래 배지, 괴물은 배지 없이 몸을 그 마법 그림의 색으로 물들인다 — 해로운 것 중
            // 가장 오래 남는 것의 색으로(사용자 결정 2026-09-18).
            foreach ((uint serial, Actor person) in _crowd)
            {
                person.Ailing(mine.AilmentsOf(serial).Select(one => one.Badge));
            }

            foreach ((uint serial, Actor beast) in _herd)
            {
                SeenAilment? worst = mine.AilmentsOf(serial)
                    .Where(one => one.Harmful && one.Effect > 0)
                    .OrderByDescending(one => one.Left)
                    .FirstOrDefault();

                beast.Tint(worst is null ? Colors.White : Flash.Tint(worst.Effect));
            }
        }
    }

    /// <summary>Plays the sounds the server asked for (0x19). The number is the file's name.</summary>
    private void Sounds()
    {
        while (server is { } world && world.TakeSound(out int number))
        {
            string path = $"res://assets/sound/{number}.mp3";

            if (!ResourceLoader.Exists(path))
            {
                Told($"소리 {number} 파일이 없습니다");
                continue;
            }

            AudioStreamPlayer? free = _voices.Find(voice => !voice.Playing);

            if (free is null)
            {
                continue;
            }

            free.Stream = GD.Load<AudioStream>(path);
            free.Play();
        }
    }

    /// <summary>
    /// Draws the body motions the server names — anybody's, ours included, since a skill's motion is only known
    /// from here. Our own plain blow is drawn as it is asked for, so that one coming back is left alone. An emote
    /// shows over a person's head (<see cref="Emote" />); a motion with no drawing moves nobody; a creature swings its
    /// own blow for any. A skill motion is drawn only in clothes skill.tbl lists for it — the original client does
    /// nothing otherwise (<see cref="BodyMotion.Fits" />).
    /// </summary>
    private void Swings()
    {
        while (server is { } world && world.TakeMotion(out Motion? motion))
        {
            // 미리 그리지 않은 평타(로그인 뒤 첫 번)는 답이 왔을 때 그린다.
            if (motion.Serial == world.Serial
                && _ownBlow.Heard(motion.Number, motion.Speed, System.TimeSpan.FromMilliseconds(Time.GetTicksMsec())))
            {
                DrawOwnBlow(motion.Number, motion.Speed);
                continue;
            }

            if ((motion.Serial == world.Serial && motion.Number == 1) || Someone(world, motion.Serial) is not { } actor)
            {
                continue;
            }

            if (BodyMotion.Of(motion.Number) is { } body
                && (_herd.ContainsKey(motion.Serial) || BodyMotion.Fits(motion.Number, ArmourOf(world, motion.Serial))))
            {
                actor.Play(body, body.SecondsPerFrame(motion.Speed));
            }
            else if (_herd.ContainsKey(motion.Serial))
            {
                actor.Strike();
            }
            else if (Emote.Of(motion.Number) is { } emote)
            {
                actor.Show(emote);
            }
            else
            {
                Told($"몸동작 {motion.Number} 을 모릅니다");
            }
        }
    }
}
