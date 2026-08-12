<?php
use Illuminate\Support\Facades\Route;

Route::get('/health', function (): array {
    return ['ready' => true];
});
