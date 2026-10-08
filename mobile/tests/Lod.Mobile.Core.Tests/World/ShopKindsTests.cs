using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>상점 창 갈래 탭(사용자 2026-10-08) — 입는 자리로 가르고, 그 상점에 있는 갈래만 탭 차례대로.</summary>
public sealed class ShopKindsTests
{
    private static ItemStats At(int place) => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, place);

    [Fact]
    public void Gloves_bracelets_and_greaves_are_one_kind_and_earrings_and_rings_another()
    {
        Assert.Equal("장갑·팔찌·각반", ShopKinds.Of(At(9)));
        Assert.Equal("장갑·팔찌·각반", ShopKinds.Of(At(10)));
        Assert.Equal("장갑·팔찌·각반", ShopKinds.Of(At(12)));
        Assert.Equal("장신구", ShopKinds.Of(At(5)));
        Assert.Equal("장신구", ShopKinds.Of(At(7)));
        Assert.Equal("장신구", ShopKinds.Of(At(8)));
        Assert.Equal("벨트", ShopKinds.Of(At(11)));
        Assert.Equal("방패", ShopKinds.Of(At(3)));
        Assert.Equal("기타", ShopKinds.Of(null));
    }

    [Fact]
    public void A_shop_shows_only_its_kinds_in_tab_order()
    {
        Assert.Equal(["갑옷", "방패", "장신구", "장갑·팔찌·각반"], ShopKinds.In([At(12), At(5), At(2), At(3), At(9), At(2)]));
        Assert.Equal(["무기"], ShopKinds.In([At(1), At(1)]));
    }
}
