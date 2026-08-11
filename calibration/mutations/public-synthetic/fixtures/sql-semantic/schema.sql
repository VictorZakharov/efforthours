CREATE TYPE order_status AS ENUM ('pending', 'paid');

CREATE TABLE orders (
    id BIGSERIAL PRIMARY KEY,
    customer_id BIGINT NOT NULL,
    status order_status NOT NULL,
    total DECIMAL(10, 2) NOT NULL,
    CONSTRAINT valid_total CHECK (total >= 0)
);

CREATE INDEX ix_orders_customer ON orders(customer_id);
CREATE VIEW paid_orders AS
SELECT id, customer_id, total
FROM orders
WHERE status::text ILIKE 'paid';

CREATE FUNCTION customer_order_total(customer BIGINT) RETURNS DECIMAL
LANGUAGE SQL AS $$
    SELECT sum(total) FROM orders WHERE customer_id = customer
$$;

WITH ranked AS (
    SELECT id, row_number() OVER (PARTITION BY customer_id ORDER BY id) AS rank
    FROM orders
)
SELECT id FROM ranked WHERE rank = 1;
