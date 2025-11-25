-- Database setup script for BiometricsFingerprint system
-- Run this script in your MySQL database

-- Create database if it doesn't exist
CREATE DATABASE IF NOT EXISTS testdb;
USE testdb;

-- Create members table
CREATE TABLE IF NOT EXISTS members (
    id INT AUTO_INCREMENT PRIMARY KEY,
    fname VARCHAR(255) NOT NULL,
    faname VARCHAR(255),
    finger_print LONGBLOB NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Insert some test data (optional)
-- INSERT INTO members (fname, faname, finger_print) VALUES ('John Doe', 'John', 'test_fingerprint_data');

-- Show table structure
DESCRIBE members;

-- Show any existing data
SELECT * FROM members;



