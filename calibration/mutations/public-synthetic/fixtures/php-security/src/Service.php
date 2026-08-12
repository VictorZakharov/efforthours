<?php
namespace App;

use Illuminate\Support\Facades\Hash;

final class Service
{
    public function handle(string $value): string
    {
        return Hash::make($value);
    }
}
