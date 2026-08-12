use sqlx::{query, Executor};

pub async fn load<'a, T: Executor<'a>>(executor: T) {
    let _rows = query("select id from orders").execute(executor).await;
}
