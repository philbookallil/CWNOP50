using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Data;

namespace Nop.Plugin.CW.Core.Services.Orders
{
    /// <summary>
    /// Custom class to deal with NOP Return Request
    /// </summary>
    public class CWReturnRequestService : ICWReturnRequestService
    {
        #region Fields

        protected readonly IRepository<ReturnRequest> _returnRequestRepository;
        protected readonly IRepository<Customer> _customerRepository;


        #endregion

        #region Ctor

        public CWReturnRequestService(IRepository<ReturnRequest> returnRequestRepository,
            IRepository<Customer> customerRepository)
        {
            _returnRequestRepository = returnRequestRepository;
            _customerRepository = customerRepository;
        }

        #endregion

        #region Methods

        public async Task<IPagedList<ReturnRequest>> SearchReturnRequestsAsync(int storeId = 0, int customerId = 0, string firstname = "", string lastname = "",
            int orderItemId = 0, string customNumber = "", ReturnRequestStatus? rs = null, DateTime? createdFromUtc = null, DateTime? createdToUtc = null,
            int pageIndex = 0, int pageSize = int.MaxValue, bool getOnlyTotalCount = false)
        {
            var customerEntityName = typeof(Customer).Name;
            var query = _returnRequestRepository.Table;
            if (storeId > 0)
                query = query.Where(rr => storeId == rr.StoreId);
            if (customerId > 0)
                query = query.Where(rr => customerId == rr.CustomerId);


            if (!string.IsNullOrEmpty(firstname))
            {
                query = from rq in query
                        join customer in _customerRepository.Table on rq.CustomerId equals customer.Id
                        where customer.FirstName.Contains(firstname)
                        select rq;
            }

            if (!string.IsNullOrEmpty(lastname))
            {
                query = from rq in query
                        join customer in _customerRepository.Table on rq.CustomerId equals customer.Id
                        where customer.LastName.Contains(lastname)
                        select rq;
            }


            if (rs.HasValue)
            {
                var returnStatusId = (int)rs.Value;
                query = query.Where(rr => rr.ReturnRequestStatusId == returnStatusId);
            }

            if (orderItemId > 0)
                query = query.Where(rr => rr.OrderItemId == orderItemId);

            if (!string.IsNullOrEmpty(customNumber))
                query = query.Where(rr => rr.CustomNumber == customNumber);

            if (createdFromUtc.HasValue)
                query = query.Where(rr => createdFromUtc.Value <= rr.CreatedOnUtc);
            if (createdToUtc.HasValue)
                query = query.Where(rr => createdToUtc.Value >= rr.CreatedOnUtc);

            query = query.OrderByDescending(rr => rr.CreatedOnUtc).ThenByDescending(rr => rr.Id);

            var returnRequests = await query.ToPagedListAsync(pageIndex, pageSize, getOnlyTotalCount);
            return returnRequests;
        }

        #endregion
    }
}
