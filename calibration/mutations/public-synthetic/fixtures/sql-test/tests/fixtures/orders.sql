BEGIN TRANSACTION;
INSERT INTO orders(id, customer_id, total) VALUES (1, 10, 25.00);
SELECT id FROM orders WHERE total = 25.00;
ROLLBACK;
