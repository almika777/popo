using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Popo.Storage;

#nullable disable

namespace Popo.Storage.Migrations;

[DbContext(typeof(PopoDbContext))]
[Migration("20260811110000_AddMoneyMarketFunds")]
public partial class AddMoneyMarketFunds
{
}
