pub fn value() -> usize {
    1
}

#[cfg(test)]
mod tests {
    #[test]
    fn returns_value() {
        assert_eq!(super::value(), 1);
    }
}
