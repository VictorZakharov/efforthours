pub struct Service;

impl Service {
    pub fn value(&self, input: usize) -> usize {
        if input > 0 { input } else { 1 }
    }
}
