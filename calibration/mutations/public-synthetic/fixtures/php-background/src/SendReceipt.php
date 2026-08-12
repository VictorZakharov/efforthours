<?php
namespace App;

use Illuminate\Contracts\Queue\ShouldQueue;

final class SendReceipt implements ShouldQueue
{
    public function handle(): void {}
}
