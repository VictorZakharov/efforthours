ATTACH DATABASE 'archive.db' AS archive;
SELECT current_order.id, archived_order.total
FROM orders AS current_order
JOIN archive.orders AS archived_order ON archived_order.id = current_order.id;
