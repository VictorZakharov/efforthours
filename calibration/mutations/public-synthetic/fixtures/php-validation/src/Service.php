<?php
namespace App;

use Illuminate\Support\Facades\Validator;

final class Service
{
    public function handle(array $input): bool
    {
        Validator::make($input, []);
        return true;
    }
}
