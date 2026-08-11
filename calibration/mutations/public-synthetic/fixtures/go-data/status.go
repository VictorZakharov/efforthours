package status

import "database/sql"

func Ready() bool { return true }

func Load(db *sql.DB) error {
	_, err := db.Query("select ready from status")
	return err
}
