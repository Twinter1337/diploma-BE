INSERT INTO public.achievements (type, title, description, icon_url) VALUES
-- Sessions
(1,  'Перший крок',           'Завершіть своє перше тренування',          'achievements/first_session.png'),
(2,  'П''ять занять',         'Завершіть 5 тренувань',                    'achievements/five_sessions.png'),
(3,  'Десятка',               'Завершіть 10 тренувань',                   'achievements/ten_sessions.png'),
(4,  'П''ятдесят занять',     'Завершіть 50 тренувань',                   'achievements/fifty_sessions.png'),
(5,  'Сотня',                 'Завершіть 100 тренувань',                  'achievements/hundred_sessions.png'),

-- Trainers
(6,  'Перший тренер',         'Потренуйтеся з першим тренером',           'achievements/first_trainer.png'),
(7,  'П''ять тренерів',       'Потренуйтеся з 5 різними тренерами',       'achievements/five_trainers.png'),
(8,  'Десять тренерів',       'Потренуйтеся з 10 різними тренерами',      'achievements/ten_trainers.png'),
(9,  'П''ятдесят тренерів',   'Потренуйтеся з 50 різними тренерами',      'achievements/fifty_trainers.png'),
(10, 'Сто тренерів',          'Потренуйтеся зі 100 різними тренерами',    'achievements/hundred_trainers.png'),

-- Specializations
(11, 'Перша спеціалізація',   'Спробуйте перший вид тренувань',           'achievements/first_spec.png'),
(12, 'П''ять напрямків',      'Спробуйте 5 різних спеціалізацій',         'achievements/five_specs.png'),
(13, 'Десять напрямків',      'Спробуйте 10 різних спеціалізацій',        'achievements/ten_specs.png'),
(14, 'П''ятдесят напрямків',  'Спробуйте 50 різних спеціалізацій',        'achievements/fifty_specs.png'),
(15, 'Сто напрямків',         'Спробуйте 100 різних спеціалізацій',       'achievements/hundred_specs.png'),

-- Cities
(16, 'Перше місто',           'Потренуйтеся у своєму першому місті',      'achievements/first_city.png'),
(17, 'П''ять міст',           'Потренуйтеся у 5 різних містах',           'achievements/five_cities.png'),
(18, 'Десять міст',           'Потренуйтеся у 10 різних містах',          'achievements/ten_cities.png'),
(19, 'Двадцять чотири міста', 'Потренуйтеся у 24 різних містах',          'achievements/twenty_four_cities.png'),

-- Loyalty
(20, 'Вірний учень',          'Проведіть 20 занять з одним тренером',     'achievements/loyal_student.png'),
(21, 'Своє місце',            'Проведіть 20 занять в одному залі',        'achievements/home_gym.png'),

-- Time
(22, 'Ранкова пташка',        'Відвідайте тренування до 8:00',            'achievements/early_bird.png'),
(23, 'Нічна сова',            'Відвідайте тренування після 20:00',        'achievements/night_owl.png'),
(24, 'Марафонець',            'Проведіть 5 тренувань за один день',       'achievements/marathon.png')
ON CONFLICT (type) DO NOTHING;
