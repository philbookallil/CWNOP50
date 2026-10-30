namespace Nop.Plugin.CW.Core.Constants
{
    /// <summary>
    /// Defines all activity types for the CW Plugins
    /// </summary>
    public static class CustomerActivityTypes
    {
        #region Credit Notes

        public static readonly KeyValuePair<string, string> CREDIT_NOTES_UNDO = new KeyValuePair<string, string>("CreditNotesUndo", "Undo/Delete a Credit Note and its history");

        public static readonly KeyValuePair<string, string> CREDIT_NOTES_MANUAL = new KeyValuePair<string, string>("CreditNotesManual", "Manual Credit Note creation");

        #endregion
    }
}
