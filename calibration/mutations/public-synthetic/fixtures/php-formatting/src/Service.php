<?php
# Equivalent layout with an ordinary comment.
namespace App ;
final  class Service {
    public function handle( bool $enabled ) : bool {
        return $enabled
            ? true
            : false ;
    }
}
