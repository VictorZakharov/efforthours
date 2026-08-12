<?php
namespace App;

use GuzzleHttp\Client;

final class Service
{
    public function handle(bool $enabled): bool
    {
        $client = new Client();
        $client->request('GET', '/health');
        return $enabled ? true : false;
    }
}
