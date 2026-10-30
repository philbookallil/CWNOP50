using Nop.Core;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Returns;

namespace Nop.Plugin.CW.Core.Services.Returns;

/// <summary>
/// Data access and aggregate invariants for the Return Case model
/// </summary>
public class ReturnCaseService : IReturnCaseService
{
    #region Fields

    protected readonly IRepository<ReturnCase> _returnCaseRepository;
    protected readonly IRepository<ReturnCaseLine> _returnCaseLineRepository;
    protected readonly IRepository<ReturnResolution> _returnResolutionRepository;
    protected readonly IRepository<ReturnCharge> _returnChargeRepository;

    #endregion

    #region Ctor

    public ReturnCaseService(IRepository<ReturnCase> returnCaseRepository,
        IRepository<ReturnCaseLine> returnCaseLineRepository,
        IRepository<ReturnResolution> returnResolutionRepository,
        IRepository<ReturnCharge> returnChargeRepository)
    {
        _returnCaseRepository = returnCaseRepository;
        _returnCaseLineRepository = returnCaseLineRepository;
        _returnResolutionRepository = returnResolutionRepository;
        _returnChargeRepository = returnChargeRepository;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Builds the case number shown to customers and staff. Matches NOP's default for return
    /// requests — the identifier. Change here if a richer format is ever wanted.
    /// </summary>
    protected virtual string GenerateCaseNumber(ReturnCase returnCase)
    {
        return returnCase.Id.ToString();
    }

    #endregion

    #region Cases

    public virtual async Task<ReturnCase> GetReturnCaseByIdAsync(int returnCaseId)
    {
        if (returnCaseId <= 0)
            return null;

        return await _returnCaseRepository.GetByIdAsync(returnCaseId);
    }

    public virtual async Task<ReturnCase> GetDraftReturnCaseAsync(int customerId, int orderId, int storeId)
    {
        if (customerId <= 0 || orderId <= 0)
            return null;

        return await _returnCaseRepository.Table
            .Where(c => c.CustomerId == customerId
                        && c.OrderId == orderId
                        && c.StoreId == storeId
                        && c.StatusId == (int)ReturnCaseStatus.Draft)
            .OrderByDescending(c => c.Id)
            .FirstOrDefaultAsync();
    }

    public virtual async Task InsertReturnCaseAsync(ReturnCase returnCase)
    {
        ArgumentNullException.ThrowIfNull(returnCase);

        var now = DateTime.UtcNow;
        returnCase.CreatedOnUtc = now;
        returnCase.UpdatedOnUtc = now;

        await _returnCaseRepository.InsertAsync(returnCase);
    }

    public virtual async Task UpdateReturnCaseAsync(ReturnCase returnCase)
    {
        ArgumentNullException.ThrowIfNull(returnCase);

        returnCase.UpdatedOnUtc = DateTime.UtcNow;

        await _returnCaseRepository.UpdateAsync(returnCase);
    }

    public virtual async Task SubmitReturnCaseAsync(ReturnCase returnCase)
    {
        ArgumentNullException.ThrowIfNull(returnCase);

        if (returnCase.Status != ReturnCaseStatus.Draft)
            throw new NopException($"Return case {returnCase.Id} cannot be submitted because it is {returnCase.Status}, not a draft");

        if (!returnCase.ReturnReasonId.HasValue || returnCase.ReturnReasonId <= 0)
            throw new NopException($"Return case {returnCase.Id} cannot be submitted without a return reason");

        if (!returnCase.ReturnActionId.HasValue || returnCase.ReturnActionId <= 0)
            throw new NopException($"Return case {returnCase.Id} cannot be submitted without a requested action");

        var lines = await GetReturnCaseLinesAsync(returnCase.Id);
        if (lines.Count == 0)
            throw new NopException($"Return case {returnCase.Id} cannot be submitted because it has no items");

        returnCase.Status = ReturnCaseStatus.Submitted;
        returnCase.SubmittedOnUtc = DateTime.UtcNow;
        returnCase.CaseNumber = GenerateCaseNumber(returnCase);

        await UpdateReturnCaseAsync(returnCase);
    }

    #endregion

    #region Lines

    public virtual async Task<IList<ReturnCaseLine>> GetReturnCaseLinesAsync(int returnCaseId)
    {
        if (returnCaseId <= 0)
            return new List<ReturnCaseLine>();

        return await _returnCaseLineRepository.Table
            .Where(l => l.ReturnCaseId == returnCaseId)
            .OrderBy(l => l.Id)
            .ToListAsync();
    }

    public virtual async Task<ReturnCaseLine> GetReturnCaseLineByReturnRequestIdAsync(int returnRequestId)
    {
        if (returnRequestId <= 0)
            return null;

        return await _returnCaseLineRepository.Table
            .Where(l => l.ReturnRequestId == returnRequestId)
            .FirstOrDefaultAsync();
    }

    public virtual async Task InsertReturnCaseLineAsync(ReturnCaseLine returnCaseLine)
    {
        ArgumentNullException.ThrowIfNull(returnCaseLine);

        await _returnCaseLineRepository.InsertAsync(returnCaseLine);
    }

    public virtual async Task UpdateReturnCaseLineAsync(ReturnCaseLine returnCaseLine)
    {
        ArgumentNullException.ThrowIfNull(returnCaseLine);

        await _returnCaseLineRepository.UpdateAsync(returnCaseLine);
    }

    public virtual async Task SetLineDispositionAsync(ReturnCaseLine returnCaseLine,
        ReturnLineDisposition disposition, string reason, int? staffCustomerId)
    {
        ArgumentNullException.ThrowIfNull(returnCaseLine);

        returnCaseLine.Disposition = disposition;
        returnCaseLine.DispositionReason = reason;
        returnCaseLine.DispositionOnUtc = DateTime.UtcNow;
        returnCaseLine.DispositionByCustomerId = staffCustomerId;

        await UpdateReturnCaseLineAsync(returnCaseLine);
    }

    #endregion

    #region Resolutions

    public virtual async Task<ReturnResolution> GetResolutionByCaseIdAsync(int returnCaseId)
    {
        if (returnCaseId <= 0)
            return null;

        return await _returnResolutionRepository.Table
            .Where(r => r.ReturnCaseId == returnCaseId)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Updates a resolution
    /// </summary>
    /// <param name="resolution"></param>
    /// <returns></returns>
    public virtual async Task UpdateResolutionAsync(ReturnResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        await _returnResolutionRepository.UpdateAsync(resolution);
    }

    public virtual async Task InsertResolutionAsync(ReturnResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        await _returnResolutionRepository.InsertAsync(resolution);
    }

    public virtual async Task InsertChargeAsync(ReturnCharge charge)
    {
        ArgumentNullException.ThrowIfNull(charge);

        await _returnChargeRepository.InsertAsync(charge);
    }

    public virtual async Task<IList<ReturnCharge>> GetChargesByResolutionIdAsync(int returnResolutionId)
    {
        if (returnResolutionId == 0)
            return [];

        return await _returnChargeRepository.Table
            .Where(charge => charge.ReturnResolutionId == returnResolutionId)
            .ToListAsync();
    }

    public virtual async Task ReopenCaseAsync(int returnCaseId)
    {
        if (returnCaseId <= 0)
            return;

        //Every resolution, not just the newest: GetResolutionByCaseIdAsync takes the highest id, so a
        //second row left behind by an earlier failure would otherwise survive and keep the case looking
        //resolved forever.
        var resolutions = await _returnResolutionRepository.Table
            .Where(resolution => resolution.ReturnCaseId == returnCaseId)
            .ToListAsync();

        //Collect the charges before anything is deleted — once the resolutions are gone there is no route
        //back to them.
        var charges = new List<ReturnCharge>();

        foreach (var resolution in resolutions)
            charges.AddRange(await GetChargesByResolutionIdAsync(resolution.Id));

        //Resolutions FIRST, deliberately. Either write can fail on its own, and the two orders fail very
        //differently. Resolution gone and charges orphaned: nothing can reach the charges, every reader
        //falls back to live pricing, and the result is correct. Charges gone and the resolution
        //surviving: the case still looks resolved and answers "no fees" for ever, silently. Orphaned rows
        //are cheaper than a case that stops charging.
        //
        //This ordering is PERMANENT, not a stand-in for a transaction. There is no transaction to be had:
        //the data layer opens a fresh connection per write and exposes no way to begin one, so several
        //writes can never be made atomic. Do not "replace this with a transaction" — there is nothing to
        //replace it with. Serialisation comes from the case lock in ReturnResolutionLogic instead; this
        //method is only ever reached through TryReopenCaseAsync.
        if (resolutions.Count > 0)
            await _returnResolutionRepository.DeleteAsync(resolutions);

        if (charges.Count > 0)
            await _returnChargeRepository.DeleteAsync(charges);

        var returnCase = await GetReturnCaseByIdAsync(returnCaseId);

        if (returnCase is not { Status: ReturnCaseStatus.Resolved })
            return;

        returnCase.Status = ReturnCaseStatus.Submitted;
        returnCase.UpdatedOnUtc = DateTime.UtcNow;

        await UpdateReturnCaseAsync(returnCase);
    }

    #endregion
}