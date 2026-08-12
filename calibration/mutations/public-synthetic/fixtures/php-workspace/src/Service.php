<?php
namespace App;

use Domain\Order;

final class Service
{
    public function handle(Order $order): Order
    {
        return $order;
    }
}
