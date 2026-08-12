use criterion::{criterion_group, Criterion};

fn benchmark(criterion: &mut Criterion) {
    criterion.bench_function("value", |bencher| bencher.iter(|| 1));
}

criterion_group!(benches, benchmark);
