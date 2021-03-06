using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Orders.Orders;
using Orders.Orders.Events;
using Core.Testing;
using FluentAssertions;
using Orders.Api.Requests.Carts;
using Orders.Products.ValueObjects;
using Shipments.Api.Tests.Core;
using Xunit;

namespace Orders.Api.Tests.Orders
{
    public class InitOrderFixture: ApiFixture<Startup>
    {
        protected override string ApiUrl { get; } = "/api/Orders";

        public readonly Guid ClientId = Guid.NewGuid();

        public readonly List<PricedProductItemRequest> ProductItems = new List<PricedProductItemRequest>
        {
            new PricedProductItemRequest {ProductId = Guid.NewGuid(), Quantity = 10, UnitPrice = 3},
            new PricedProductItemRequest {ProductId = Guid.NewGuid(), Quantity = 3, UnitPrice = 7}
        };

        public decimal TotalPrice => ProductItems.Sum(pi => pi.Quantity * pi.UnitPrice);

        public readonly DateTime TimeBeforeSending = DateTime.UtcNow;

        public HttpResponseMessage CommandResponse;

        public override async Task InitializeAsync()
        {
            CommandResponse = await PostAsync(new InitOrderRequest
            {
                ClientId = ClientId, ProductItems = ProductItems, TotalPrice = TotalPrice
            });
        }
    }

    public class InitOrderTests: IClassFixture<InitOrderFixture>
    {
        private readonly InitOrderFixture fixture;

        public InitOrderTests(InitOrderFixture fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        [Trait("Category", "Exercise")]
        public async Task CreateCommand_ShouldReturn_CreatedStatus_With_OrderId()
        {
            var commandResponse = fixture.CommandResponse;
            commandResponse.EnsureSuccessStatusCode();
            commandResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            // get created record id
            var commandResult = await commandResponse.Content.ReadAsStringAsync();
            commandResult.Should().NotBeNull();

            var createdId = commandResult.FromJson<Guid>();
            createdId.Should().NotBeEmpty();
        }

        [Fact]
        [Trait("Category", "Exercise")]
        public async Task CreateCommand_ShouldPublish_OrderInitializedEvent()
        {
            var createdId = await fixture.CommandResponse.GetResultFromJSON<Guid>();

            fixture.PublishedInternalEventsOfType<OrderInitialized>()
                .Should()
                .HaveCount(1)
                .And.Contain(@event =>
                    @event.OrderId == createdId
                    && @event.ClientId == fixture.ClientId
                    && @event.InitializedAt > fixture.TimeBeforeSending
                    && @event.ProductItems.Count == fixture.ProductItems.Count
                    && @event.ProductItems.All(
                        pi => fixture.ProductItems.Exists(
                            expi => expi.ProductId == pi.ProductId && expi.Quantity == pi.Quantity))
                );
        }

        // [Fact]
        // [Trait("Category", "Exercise")]
        // public async Task CreateCommand_ShouldCreate_Order()
        // {
        //     var createdId = await fixture.CommandResponse.GetResultFromJSON<Guid>();
        //
        //     // prepare query
        //     var query = $"{createdId}";
        //
        //     //send query
        //     var queryResponse = await fixture.GetAsync(query);
        //     queryResponse.EnsureSuccessStatusCode();
        //
        //     var queryResult = await queryResponse.Content.ReadAsStringAsync();
        //     queryResult.Should().NotBeNull();
        //
        //     var OrderDetails = queryResult.FromJson<Order>();
        //     OrderDetails.Id.Should().Be(createdId);
        //     OrderDetails.OrderId.Should().Be(fixture.ClientId);
        //     OrderDetails.SentAt.Should().BeAfter(fixture.TimeBeforeSending);
        //     OrderDetails.ProductItems.Should().NotBeEmpty();
        //     OrderDetails.ProductItems.All(
        //         pi => fixture.ProductItems.Exists(
        //             expi => expi.ProductId == pi.ProductId && expi.Quantity == pi.Quantity))
        //         .Should().BeTrue();
        // }
    }
}
