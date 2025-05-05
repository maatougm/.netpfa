-- Table for location working hours
CREATE TABLE IF NOT EXISTS location_hours (
    location_id INT NOT NULL,
    start_hour TIME NOT NULL,
    end_hour TIME NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (location_id),
    FOREIGN KEY (location_id) REFERENCES locations(id)
);

-- Table for special days (freeze days)
CREATE TABLE IF NOT EXISTS special_day (
    id INT AUTO_INCREMENT PRIMARY KEY,
    date DATE NOT NULL,
    start_time TIME NULL,
    end_time TIME NULL,
    is_whole_day TINYINT(1) DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    location_id INT NULL,
    FOREIGN KEY (location_id) REFERENCES locations(id)
);

-- Table for freeze days
CREATE TABLE IF NOT EXISTS freeze_days (
    id INT AUTO_INCREMENT PRIMARY KEY,
    date DATE NOT NULL,
    start_hour TIME NOT NULL,
    end_hour TIME NOT NULL,
    reason VARCHAR(255),
    status ENUM('active', 'inactive') DEFAULT 'active',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY unique_date_hours (date, start_hour, end_hour)
);

-- Table for general settings
CREATE TABLE IF NOT EXISTS settings (
    id INT AUTO_INCREMENT PRIMARY KEY,
    allow_cancellations BOOLEAN DEFAULT TRUE,
    cancellation_hours INT DEFAULT 24,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

-- Insert default settings if not exists
INSERT IGNORE INTO settings (id, allow_cancellations, cancellation_hours)
VALUES (1, TRUE, 24); 