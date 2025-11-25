-- Clear corrupted fingerprint data
USE biometric;

-- Delete all existing corrupted records
DELETE FROM register_student;

-- Reset auto increment
ALTER TABLE register_student AUTO_INCREMENT = 1;

-- Show the result
SELECT COUNT(*) as remaining_records FROM register_student;
