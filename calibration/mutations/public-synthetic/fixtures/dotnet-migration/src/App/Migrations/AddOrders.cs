namespace EffortHoursSynthetic.Migrations;

public sealed class AddOrders : Migration
{
    public void Up() => ExecuteSql("create table orders");
}
