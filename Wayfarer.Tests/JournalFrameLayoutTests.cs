using Wayfarer.Core.Ui;

namespace Wayfarer.Tests;

/// <summary>The gilt border's proof: it closes, at every height, out of the pieces the game uses.
///
/// <para>The border is ornament, and ornament that lands slightly wrong is the single most "this is
/// not the game" thing a plugin can draw. These assertions are what stops a future edit from moving
/// a piece: the horizontal run has to tile 496 exactly, the vertical run has to be continuous, and
/// only the two rails may ever change size.</para></summary>
public class JournalFrameLayoutTests
{
    public static TheoryData<float> Heights =>
    [
        0f, 100f, 288f, 300f, 420f,
        GameMetrics.JournalFrame.AuthoredHeight, 900f, 1400f,
    ];

    [Theory]
    [MemberData(nameof(Heights))]
    public void The_border_is_sixteen_pieces_and_only_the_rails_stretch(float height)
    {
        var pieces = JournalFrameLayout.Pieces(height);

        Assert.Equal(16, pieces.Count);
        Assert.Equal(2, pieces.Count(piece => piece.Stretches));
        Assert.Equal(5, pieces.Count(piece => piece.FlipHorizontally));
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void Every_piece_samples_a_rectangle_the_size_it_is_drawn_at(float height)
    {
        // The one exception is a rail, which is a nine-grid: it samples a 32x40 part and is drawn at
        // whatever height is left. Everything else is a plain image, and a plain image whose part
        // rectangle disagrees with its node size is the smear this project has shipped before.
        foreach (var piece in JournalFrameLayout.Pieces(height).Where(piece => !piece.Stretches && !piece.IsEmpty))
        {
            Assert.Equal(piece.Destination.Width, piece.Source.Width);
            Assert.Equal(piece.Destination.Height, piece.Source.Height);
        }
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void Every_piece_samples_inside_the_texture(float height)
    {
        // ui/uld/Journal_Frame.tex, extracted and viewed: 240x192, A8R8G8B8.
        var sheet = new ScreenRect(0f, 0f, 240f, 192f);

        foreach (var piece in JournalFrameLayout.Pieces(height))
        {
            Assert.True(piece.Source.ContainedBy(sheet), $"{piece.SourceNode} samples {piece.Source}");
        }
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void Every_piece_stays_inside_the_frame(float height)
    {
        var frame = new ScreenRect(0f, 0f, GameMetrics.JournalFrame.Width, Math.Max(height, 0f));

        foreach (var piece in JournalFrameLayout.Pieces(height))
        {
            Assert.True(piece.Destination.ContainedBy(frame), $"{piece.SourceNode} at {piece.Destination}");
        }
    }

    [Fact]
    public void The_top_run_tiles_the_authored_width_with_no_gap()
    {
        // 0..56 corner, 56..160 run, 160..208 bar, 208..288 boss, 288..336 bar, 336..440 run,
        // 440..496 corner: JournalDetail #18/#16/#17/#15/#28/#27/#26, in that order.
        var top = JournalFrameLayout
            .Pieces(GameMetrics.JournalFrame.AuthoredHeight)
            .Where(piece => piece.Destination.Y < GameMetrics.JournalFrame.RailWidth)
            .OrderBy(piece => piece.Destination.X)
            .ToList();

        Assert.Equal(0f, top[0].Destination.X);
        for (var i = 1; i < top.Count; i++)
        {
            Assert.Equal(top[i - 1].Destination.Right, top[i].Destination.X);
        }

        Assert.Equal(GameMetrics.JournalFrame.Width, top[^1].Destination.Right);
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void The_side_runs_are_continuous_from_the_top_corner_to_the_bottom_one(float height)
    {
        if (height < GameMetrics.JournalFrame.MinHeight)
        {
            return;
        }

        var pieces = JournalFrameLayout.Pieces(height);
        var rail = pieces.Single(piece => piece.Stretches && piece.Destination.X == 0f);
        var upper = pieces.Single(piece => piece.SourceNode == 19);
        var foot = pieces.Single(piece => piece.SourceNode == 20);

        Assert.Equal(upper.Destination.Bottom, rail.Destination.Y);
        Assert.Equal(rail.Destination.Bottom, foot.Destination.Y);
        Assert.Equal(height, foot.Destination.Bottom);
    }

    [Theory]
    [MemberData(nameof(Heights))]
    public void The_border_is_mirror_symmetrical_about_the_pages_centre(float height)
    {
        if (height < GameMetrics.JournalFrame.MinHeight)
        {
            return;
        }

        var pieces = JournalFrameLayout.Pieces(height);
        var centre = GameMetrics.JournalFrame.Width / 2f;

        foreach (var piece in pieces)
        {
            var mirrored = new ScreenRect(
                GameMetrics.JournalFrame.Width - piece.Destination.Right,
                piece.Destination.Y,
                piece.Destination.Width,
                piece.Destination.Height);

            Assert.Contains(mirrored, pieces.Select(other => other.Destination));
        }

        // And the centre boss really is centred, which is what makes the two halves meet.
        var boss = pieces.Single(piece => piece.SourceNode == 15);
        Assert.Equal(centre, boss.Destination.X + (boss.Destination.Width / 2f));
    }

    [Fact]
    public void The_parchment_starts_ten_pixels_down_the_border_and_runs_to_its_foot()
    {
        var height = GameMetrics.JournalFrame.AuthoredHeight;
        var parchment = JournalFrameLayout.Parchment(height);

        Assert.Equal(GameMetrics.JournalFrame.ParchmentTop, parchment.Y);
        Assert.Equal(height, parchment.Bottom);
        Assert.Equal(GameMetrics.JournalFrame.Width, parchment.Width);
    }

    [Fact]
    public void The_authored_height_reproduces_the_games_own_rail()
    {
        // JournalDetail #14 is 32x340 at y=192. If this stops being true the border has been moved.
        var rail = JournalFrameLayout
            .Pieces(GameMetrics.JournalFrame.AuthoredHeight)
            .Single(piece => piece.Stretches && piece.Destination.X == 0f);

        Assert.Equal(new ScreenRect(0f, 192f, 32f, 340f), rail.Destination);
    }
}
