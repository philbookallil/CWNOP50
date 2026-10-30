namespace Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;

public record PackNameListsModel
{
    public int BranchId { get; set; }

    //THE INLINE ADD POSTS THIS. PackNameListValidator requires StoreId NotNull and NotEqual(0), and
    //FluentValidation runs at model binding - before SavePackNameList gets to derive the store from
    //the posted branch. Without it every inline add was rejected before the controller ran, and the
    //error is keyed to StoreId, for which the partial renders no span and no validation summary, so
    //the refusal appeared nowhere at all.
    public int StoreId { get; set; }

    public List<PackNameListRowModel> Lists { get; set; } = new();
}
