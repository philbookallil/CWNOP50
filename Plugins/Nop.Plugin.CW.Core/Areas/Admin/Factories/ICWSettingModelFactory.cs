using Nop.Plugin.CW.Core.Areas.Admin.Models.Settings;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public interface ICWSettingModelFactory
{
    /// <summary>
    /// Prepare multiStore basic settings model
    /// </summary>
    /// <param name="model">MultiStore model</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the multiStore model
    /// </returns>
    Task PrepareMultiStoreBasicSettingsModelAsync(MultiStoreSettingsModel model, int storeId);

    /// <summary>
    /// Prepare multiStore settings model
    /// </summary>
    /// <param name="model">MultiStore model</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the multiStore model
    /// </returns>
    Task<MultiStoreSettingsModel> PrepareMultiStoreSettingsModelAsync(MultiStoreSettingsModel model, int storeId);
}
