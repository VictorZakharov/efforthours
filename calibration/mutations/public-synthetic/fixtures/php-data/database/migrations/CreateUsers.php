<?php
use Illuminate\Database\Migrations\Migration;
use Illuminate\Support\Facades\Schema;

final class CreateUsers extends Migration
{
    public function up(): void
    {
        Schema::create('users', function ($table): void {});
    }
}
