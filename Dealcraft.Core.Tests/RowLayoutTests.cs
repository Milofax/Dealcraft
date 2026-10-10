using Dealcraft.Core;
using Xunit;

namespace Dealcraft.Core.Tests;

/// <summary>
/// How tall a row of the app is. The word-dropping rules that used to sit beside
/// these went with the master list they were written for.
/// </summary>
public class RowLayoutTests
{
    [Fact]
    public void A_row_is_taller_than_the_line_of_type_in_it()
    {
        // A row exactly one line tall lets a descender of one name touch the top
        // of the next, which is how the first build's tiles came to look like one
        // block of text.
        Assert.True(RowLayout.Height(20f) > 20f);
        Assert.Equal(36f, RowLayout.Height(20f), 3);
    }

    [Fact]
    public void A_row_has_a_floor_so_a_tiny_type_size_still_leaves_a_row()
    {
        Assert.Equal(RowLayout.ShortestRow, RowLayout.Height(1f), 3);
        Assert.Equal(RowLayout.ShortestRow, RowLayout.Height(0f), 3);
        Assert.Equal(RowLayout.ShortestRow, RowLayout.Height(float.NaN), 3);
    }
}
