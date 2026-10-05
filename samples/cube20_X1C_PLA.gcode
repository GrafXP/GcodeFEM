; HEADER_BLOCK_START
; BambuStudio 02.08.02.61
; model printing time: 8m 36s; total estimated time: 15m 52s
; total layer number: 100
; total filament length [mm] : 1316.16
; total filament volume [cm^3] : 3165.73
; total filament weight [g] : 3.99
; filament_density: 1.26
; filament_diameter: 1.75
; max_z_height: 20.00
; filament: 1
; support_material_on_wipe_tower: 0
; HEADER_BLOCK_END

; CONFIG_BLOCK_START
; accel_to_decel_enable = 0
; accel_to_decel_factor = 50%
; activate_air_filtration = 0
; additional_cooling_fan_speed = 70
; additional_fan_full_speed_layer = 0
; alternate_extra_wall = 0
; ams_filament_load_time_ams = 0
; ams_filament_load_time_ams_lite = 0
; ams_filament_load_time_n3f_s = 0
; ams_filament_unload_time_ams = 0
; ams_filament_unload_time_ams_lite = 0
; ams_filament_unload_time_n3f_s = 0
; apply_scarf_seam_on_circles = 1
; auxiliary_fan = 1
; avoid_crossing_wall_includes_support = 0
; bed_custom_model = 
; bed_custom_texture = 
; bed_exclude_area = 0x0,18x0,18x28,0x28
; bed_heat_soak_area = 
; bed_temperature_formula = by_first_filament
; before_layer_change_gcode = 
; best_object_pos = 0.5,0.5
; bottom_color_penetration_layers = 3
; bottom_shell_layers = 3
; bottom_shell_thickness = 0
; bottom_surface_density = 100%
; bottom_surface_pattern = monotonic
; bridge_angle = 0
; bridge_flow = 1
; bridge_no_support = 0
; bridge_speed = 50
; brim_object_gap = 0.1
; brim_type = auto_brim
; brim_width = 5
; chamber_temperatures = 0
; change_filament_gcode = ;=X1 20251031=\nM620 S[next_extruder]A\nM204 S9000\nG1 Z{max_layer_z + 3.0} F1200\n\nG1 X70 F21000\nG1 Y245\nG1 Y265 F3000\nM400\nM106 P1 S0\nM106 P2 S0\n{if old_filament_temp > 142 && next_extruder < 255}\nM104 S[old_filament_temp]\n{endif}\n{if long_retractions_when_cut[previous_extruder]}\nM620.11 S1 I[previous_extruder] E-{retraction_distances_when_cut[previous_extruder]} F{flush_volumetric_speeds[previous_extruder]/2.4053*60}\n{else}\nM620.11 S0\n{endif}\nM400\nG1 X90 F3000\nG1 Y255 F4000\nG1 X100 F5000\nG1 X120 F15000\nG1 X20 Y50 F21000\nG1 Y-3\n{if toolchange_count == 2}\n; get travel path for change filament\nM620.1 X[travel_point_1_x] Y[travel_point_1_y] F21000 P0\nM620.1 X[travel_point_2_x] Y[travel_point_2_y] F21000 P1\nM620.1 X[travel_point_3_x] Y[travel_point_3_y] F21000 P2\n{endif}\nM620.1 E F{flush_volumetric_speeds[previous_extruder]/2.4053*60} T{flush_temperatures[previous_extruder]}\nT[next_extruder]\nM620.1 E F{flush_volumetric_speeds[next_extruder]/2.4053*60} T{flush_temperatures[next_extruder]}\n\n{if next_extruder < 255}\n{if long_retractions_when_cut[previous_extruder]}\nM620.11 S1 I[previous_extruder] E{retraction_distances_when_cut[previous_extruder]} F{flush_volumetric_speeds[previous_extruder]/2.4053*60}\nM628 S1\nG92 E0\nG1 E{retraction_distances_when_cut[previous_extruder]} F{flush_volumetric_speeds[previous_extruder]/2.4053*60}\nM400\nM629 S1\n{else}\nM620.11 S0\n{endif}\nG92 E0\n{if flush_length_1 > 1}\nM83\n; FLUSH_START\n; always use highest temperature to flush\nM400\n{if filament_type[next_extruder] == \"PETG\"}\nM109 S260\n{elsif filament_type[next_extruder] == \"PVA\"}\nM109 S210\n{else}\nM109 S{flush_temperatures[next_extruder]}\n{endif}\n{if flush_length_1 > 23.7}\nG1 E23.7 F{flush_volumetric_speeds[previous_extruder]/2.4053*60} ; do not need pulsatile flushing for start part\nG1 E{(flush_length_1 - 23.7) * 0.02} F50\nG1 E{(flush_length_1 - 23.7) * 0.23} F{flush_volumetric_speeds[previous_extruder]/2.4053*60}\nG1 E{(flush_length_1 - 23.7) * 0.02} F50\nG1 E{(flush_length_1 - 23.7) * 0.23} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{(flush_length_1 - 23.7) * 0.02} F50\nG1 E{(flush_length_1 - 23.7) * 0.23} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{(flush_length_1 - 23.7) * 0.02} F50\nG1 E{(flush_length_1 - 23.7) * 0.23} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\n{else}\nG1 E{flush_length_1} F{flush_volumetric_speeds[previous_extruder]/2.4053*60}\n{endif}\n; FLUSH_END\nG1 E-[old_retract_length_toolchange] F1800\nG1 E[old_retract_length_toolchange] F300\n{endif}\n\n{if flush_length_2 > 1}\n\nG91\nG1 X3 F12000; move aside to extrude\nG90\nM83\n\n; FLUSH_START\nG1 E{flush_length_2 * 0.18} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{flush_length_2 * 0.02} F50\nG1 E{flush_length_2 * 0.18} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{flush_length_2 * 0.02} F50\nG1 E{flush_length_2 * 0.18} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{flush_length_2 * 0.02} F50\nG1 E{flush_length_2 * 0.18} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{flush_length_2 * 0.02} F50\nG1 E{flush_length_2 * 0.18} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{flush_length_2 * 0.02} F50\n; FLUSH_END\nG1 E-[new_retract_length_toolchange] F1800\nG1 E[new_retract_length_toolchange] F300\n{endif}\n\n{if flush_length_3 > 1}\n\nG91\nG1 X3 F12000; move aside to extrude\nG90\nM83\n\n; FLUSH_START\nG1 E{flush_length_3 * 0.18} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{flush_length_3 * 0.02} F50\nG1 E{flush_length_3 * 0.18} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{flush_length_3 * 0.02} F50\nG1 E{flush_length_3 * 0.18} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{flush_length_3 * 0.02} F50\nG1 E{flush_length_3 * 0.18} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{flush_length_3 * 0.02} F50\nG1 E{flush_length_3 * 0.18} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{flush_length_3 * 0.02} F50\n; FLUSH_END\nG1 E-[new_retract_length_toolchange] F1800\nG1 E[new_retract_length_toolchange] F300\n{endif}\n\n{if flush_length_4 > 1}\n\nG91\nG1 X3 F12000; move aside to extrude\nG90\nM83\n\n; FLUSH_START\nG1 E{flush_length_4 * 0.18} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{flush_length_4 * 0.02} F50\nG1 E{flush_length_4 * 0.18} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{flush_length_4 * 0.02} F50\nG1 E{flush_length_4 * 0.18} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{flush_length_4 * 0.02} F50\nG1 E{flush_length_4 * 0.18} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{flush_length_4 * 0.02} F50\nG1 E{flush_length_4 * 0.18} F{flush_volumetric_speeds[next_extruder]/2.4053*60}\nG1 E{flush_length_4 * 0.02} F50\n; FLUSH_END\n{endif}\n; FLUSH_START\nM400\nM109 S[new_filament_temp]\nG1 E2 F{flush_volumetric_speeds[next_extruder]/2.4053*60} ;Compensate for filament spillage during waiting temperature\n; FLUSH_END\nM400\nG92 E0\nG1 E-[new_retract_length_toolchange] F1800\nM106 P1 S255\nM400 S3\n\nG1 X70 F5000\nG1 X90 F3000\nG1 Y255 F4000\nG1 X105 F5000\nG1 Y265 F5000\nG1 X70 F10000\nG1 X100 F5000\nG1 X70 F10000\nG1 X100 F5000\n\nG1 X70 F10000\nG1 X80 F15000\nG1 X60\nG1 X80\nG1 X60\nG1 X80 ; shake to put down garbage\nG1 X100 F5000\nG1 X165 F15000; wipe and shake\nG1 Y256 ; move Y to aside, prevent collision\nM400\nG1 Z{max_layer_z + 3.0} F3000\n{if layer_z <= (initial_layer_print_height + 0.001)}\nM204 S[initial_layer_acceleration]\n{else}\nM204 S[default_acceleration]\n{endif}\n{else}\nG1 X[x_after_toolchange] Y[y_after_toolchange] Z[z_after_toolchange] F12000\n{endif}\nM621 S[next_extruder]A\n
; circle_compensation_manual_offset = 0
; circle_compensation_speed = 200
; close_additional_fan_first_x_layers = 1
; close_fan_the_first_x_layers = 1
; compatible_printers_condition = 
; complete_print_exhaust_fan_speed = 70
; cool_plate_temp = 35
; cool_plate_temp_initial_layer = 35
; cooling_filter_enabled = 0
; cooling_perimeter_transition_distance = 10
; cooling_slowdown_logic = uniform_cooling
; counter_coef_1 = 0
; counter_coef_2 = 0.008
; counter_coef_3 = -0.041
; counter_limit_max = 0.033
; counter_limit_min = -0.035
; counterbore_hole_bridging = none
; curr_bed_type = Cool Plate
; default_acceleration = 10000
; default_ams_type = -1
; default_filament_colour = ""
; default_filament_profile = "Bambu PLA Basic @BBL X1C"
; default_jerk = 0
; default_nozzle_volume_type = Standard
; default_print_profile = 0.20mm Standard @BBL X1C
; deretraction_speed = 30
; detect_floating_vertical_shell = 1
; detect_narrow_internal_solid_infill = 1
; detect_overhang_wall = 1
; detect_thin_wall = 0
; diameter_limit = 50
; different_settings_to_system = ;;
; draft_shield = disabled
; during_print_exhaust_fan_speed = 70
; elefant_foot_compensation = 0.15
; embedding_wall_into_infill = 0
; enable_arc_fitting = 1
; enable_circle_compensation = 0
; enable_filament_dynamic_map = 0
; enable_height_slowdown = 0
; enable_long_retraction_when_cut = 2
; enable_mixed_color_sublayer = 0
; enable_order_independent_overlap_carving = 0
; enable_overhang_bridge_fan = 1
; enable_overhang_speed = 1
; enable_pre_heating = 0
; enable_pressure_advance = 0
; enable_prime_tower = 1
; enable_support = 0
; enable_support_ironing = 0
; enable_tower_interface_features = 0
; enable_wrapping_detection = 0
; enforce_support_layers = 0
; eng_plate_temp = 0
; eng_plate_temp_initial_layer = 0
; ensure_vertical_shell_thickness = enabled
; exclude_object = 1
; extruder_ams_count = 
; extruder_clearance_dist_to_rod = 33
; extruder_clearance_height_to_lid = 90
; extruder_clearance_height_to_rod = 34
; extruder_clearance_max_radius = 68
; extruder_colour = #018001
; extruder_max_nozzle_count = 1
; extruder_nozzle_stats = 
; extruder_offset = 0x2
; extruder_printable_area = 
; extruder_type = Direct Drive
; extruder_variant_list = "Direct Drive Standard,Direct Drive High Flow"
; fan_cooling_layer_time = 100
; fan_direction = left
; fan_max_speed = 100
; fan_min_speed = 100
; farthest_point_timelapse = 1
; filament_adaptive_volumetric_speed = 0
; filament_adhesiveness_category = 100
; filament_bridge_speed = 25
; filament_change_length = 5
; filament_change_length_nc = 10
; filament_colour = #00AE42
; filament_cooling_before_tower = 0
; filament_cost = 19.99
; filament_density = 1.26
; filament_dev_ams_drying_ams_limitations = 1
; filament_dev_ams_drying_heat_distortion_temperature = 45
; filament_dev_ams_drying_temperature = 45
; filament_dev_ams_drying_time = 12
; filament_dev_chamber_drying_bed_temperature = 70
; filament_dev_chamber_drying_time = 12
; filament_dev_drying_cooling_temperature = 45
; filament_dev_drying_softening_temperature = 50
; filament_diameter = 1.75
; filament_enable_overhang_speed = 1
; filament_end_gcode = "; filament end gcode \n\n"
; filament_extruder_compatibility = 0
; filament_extruder_variant = "Direct Drive Standard"
; filament_flow_ratio = 0.98
; filament_flush_temp = 0
; filament_flush_temp_fast = 0
; filament_flush_volumetric_speed = 0
; filament_ids = GFA00
; filament_is_mixed = 0
; filament_is_support = 0
; filament_long_retractions_when_cut = 1
; filament_map = 1
; filament_map_2 = 0
; filament_map_mode = Auto For Flush
; filament_max_volumetric_speed = 21
; filament_metal_stickiness = None
; filament_minimal_purge_on_wipe_tower = 15
; filament_mixed_components = ""
; filament_mixed_gradient = 0
; filament_mixed_gradient_curve = ""
; filament_mixed_gradient_per_part = 0
; filament_mixed_gradient_range = ""
; filament_mixed_sublayer_ratios = ""
; filament_notes = 
; filament_nozzle_map = 0
; filament_overhang_1_4_speed = 0
; filament_overhang_2_4_speed = 50
; filament_overhang_3_4_speed = 30
; filament_overhang_4_4_speed = 10
; filament_overhang_totally_speed = 10
; filament_pre_cooling_temperature = 0
; filament_pre_cooling_temperature_nc = 0
; filament_preheat_temperature_delta = 10
; filament_prime_volume = 30
; filament_prime_volume_nc = 60
; filament_printable = 3
; filament_ramming_travel_time = 0
; filament_ramming_travel_time_nc = 0
; filament_ramming_volumetric_speed = -1
; filament_ramming_volumetric_speed_nc = -1
; filament_retract_length_nc = 14
; filament_retraction_distances_when_cut = 18
; filament_scarf_gap = 0%
; filament_scarf_height = 10%
; filament_scarf_length = 10
; filament_scarf_seam_type = none
; filament_self_index = 1
; filament_settings_id = "Bambu PLA Basic @BBL X1C"
; filament_shrink = 100%
; filament_soluble = 0
; filament_start_gcode = "; filament start gcode\n{if  (bed_temperature[current_extruder] >55)||(bed_temperature_initial_layer[current_extruder] >55)}M106 P3 S200\n{elsif(bed_temperature[current_extruder] >50)||(bed_temperature_initial_layer[current_extruder] >50)}M106 P3 S150\n{elsif(bed_temperature[current_extruder] >45)||(bed_temperature_initial_layer[current_extruder] >45)}M106 P3 S50\n{endif}\nM142 P1 R35 S40\n{if activate_air_filtration[current_extruder] && support_air_filtration}\nM106 P3 S{during_print_exhaust_fan_speed_num[current_extruder]} \n{endif}"
; filament_tower_interface_pre_extrusion_dist = 10
; filament_tower_interface_pre_extrusion_length = 0
; filament_tower_interface_print_temp = -1
; filament_tower_interface_purge_volume = 20
; filament_tower_ironing_area = 4
; filament_type = PLA
; filament_velocity_adaptation_factor = 1
; filament_vendor = "Bambu Lab"
; filament_volume_map = 0
; filename_format = {input_filename_base}_{filament_type[0]}_{print_time}.gcode
; fill_multiline = 1
; filter_out_gap_fill = 0
; first_layer_print_sequence = 0
; first_x_layer_fan_speed = 0
; first_x_layer_part_fan_speed = 0
; flush_into_infill = 0
; flush_into_objects = 0
; flush_into_support = 1
; flush_multiplier = 1
; flush_multiplier_fast = 1.2
; flush_volumes_matrix = 0,280,280,280,280,0,280,280,280,280,0,280,280,280,280,0
; flush_volumes_vector = 140,140,140,140,140,140,140,140
; full_fan_speed_layer = 0
; fuzzy_skin = none
; fuzzy_skin_first_layer = 0
; fuzzy_skin_mode = displacement
; fuzzy_skin_noise_type = classic
; fuzzy_skin_octaves = 4
; fuzzy_skin_persistence = 0.5
; fuzzy_skin_point_distance = 0.8
; fuzzy_skin_scale = 1
; fuzzy_skin_thickness = 0.3
; gap_infill_speed = 250
; gcode_add_line_number = 0
; gcode_flavor = marlin
; grab_length = 0
; group_algo_with_time = 0
; has_filament_switcher = 0
; has_scarf_joint_seam = 0
; head_wrap_detect_zone = 
; hole_coef_1 = 0
; hole_coef_2 = -0.008
; hole_coef_3 = 0.23415
; hole_limit_max = 0.22
; hole_limit_min = 0.088
; hot_plate_temp = 55
; hot_plate_temp_initial_layer = 55
; hotend_cooling_rate = 2
; hotend_heating_rate = 2
; impact_strength_z = 13.8
; independent_support_layer_height = 1
; infill_combination = 0
; infill_direction = 45
; infill_instead_top_bottom_surfaces = 0
; infill_jerk = 9
; infill_lock_depth = 1
; infill_rotate_step = 0
; infill_shift_step = 0.4
; infill_wall_overlap = 15%
; inherits_group = ;;
; initial_layer_acceleration = 500
; initial_layer_flow_ratio = 1
; initial_layer_infill_speed = 105
; initial_layer_jerk = 9
; initial_layer_line_width = 0.5
; initial_layer_print_height = 0.2
; initial_layer_speed = 50
; initial_layer_travel_acceleration = 6000
; inner_wall_acceleration = 0
; inner_wall_jerk = 9
; inner_wall_line_width = 0.45
; inner_wall_speed = 300
; interface_shells = 0
; interlocking_beam = 0
; interlocking_beam_layer_count = 2
; interlocking_beam_width = 0.8
; interlocking_boundary_avoidance = 2
; interlocking_depth = 2
; interlocking_orientation = 22.5
; internal_bridge_support_thickness = 0.8
; internal_solid_infill_line_width = 0.42
; internal_solid_infill_pattern = zig-zag
; internal_solid_infill_speed = 250
; ironing_direction = 45
; ironing_fan_speed = -1
; ironing_flow = 10%
; ironing_inset = 0.21
; ironing_pattern = zig-zag
; ironing_spacing = 0.15
; ironing_speed = 30
; ironing_type = no ironing
; is_infill_first = 0
; layer_change_gcode = ; layer num/total_layer_count: {layer_num+1}/[total_layer_count]\n; update layer progress\nM73 L{layer_num+1}\nM991 S0 P{layer_num} ;notify layer change
; layer_height = 0.2
; line_width = 0.42
; locked_skeleton_infill_pattern = zigzag
; locked_skin_infill_pattern = crosszag
; long_retractions_when_cut = 0
; long_retractions_when_ec = 0
; machine_bed_mass_Y = 0
; machine_end_gcode = ;===== date: 20240528 =====================\nM400 ; wait for buffer to clear\nG92 E0 ; zero the extruder\nG1 E-0.8 F1800 ; retract\nG1 Z{max_layer_z + 0.5} F900 ; lower z a little\nG1 X65 Y245 F12000 ; move to safe pos\nG1 Y265 F3000\n\nG1 X65 Y245 F12000\nG1 Y265 F3000\nM140 S0 ; turn off bed\nM106 S0 ; turn off fan\nM106 P2 S0 ; turn off remote part cooling fan\nM106 P3 S0 ; turn off chamber cooling fan\n\nG1 X100 F12000 ; wipe\n; pull back filament to AMS\nM620 S255\nG1 X20 Y50 F12000\nG1 Y-3\nT255\nG1 X65 F12000\nG1 Y265\nG1 X100 F12000 ; wipe\nM621 S255\nM104 S0 ; turn off hotend\n\nM622.1 S1 ; for prev firware, default turned on\nM1002 judge_flag timelapse_record_flag\nM622 J1\n    M400 ; wait all motion done\n    M991 S0 P-1 ;end smooth timelapse at safe pos\n    M400 S3 ;wait for last picture to be taken\nM623; end of \"timelapse_record_flag\"\n\nM400 ; wait all motion done\nM17 S\nM17 Z0.4 ; lower z motor current to reduce impact if there is something in the bottom\n{if (max_layer_z + 100.0) < 250}\n    G1 Z{max_layer_z + 100.0} F600\n    G1 Z{max_layer_z +98.0}\n{else}\n    G1 Z250 F600\n    G1 Z248\n{endif}\nM400 P100\nM17 R ; restore z current\n\nM220 S100  ; Reset feedrate magnitude\nM201.2 K1.0 ; Reset acc magnitude\nM73.2   R1.0 ;Reset left time magnitude\nM1002 set_gcode_claim_speed_level : 0\n;=====printer finish  sound=========\nM17\nM400 S1\nM1006 S1\nM1006 A0 B20 L100 C37 D20 M40 E42 F20 N60\nM1006 A0 B10 L100 C44 D10 M60 E44 F10 N60\nM1006 A0 B10 L100 C46 D10 M80 E46 F10 N80\nM1006 A44 B20 L100 C39 D20 M60 E48 F20 N60\nM1006 A0 B10 L100 C44 D10 M60 E44 F10 N60\nM1006 A0 B10 L100 C0 D10 M60 E0 F10 N60\nM1006 A0 B10 L100 C39 D10 M60 E39 F10 N60\nM1006 A0 B10 L100 C0 D10 M60 E0 F10 N60\nM1006 A0 B10 L100 C44 D10 M60 E44 F10 N60\nM1006 A0 B10 L100 C0 D10 M60 E0 F10 N60\nM1006 A0 B10 L100 C39 D10 M60 E39 F10 N60\nM1006 A0 B10 L100 C0 D10 M60 E0 F10 N60\nM1006 A0 B10 L100 C48 D10 M60 E44 F10 N100\nM1006 A0 B10 L100 C0 D10 M60 E0 F10  N100\nM1006 A49 B20 L100 C44 D20 M100 E41 F20 N100\nM1006 A0 B20 L100 C0 D20 M60 E0 F20 N100\nM1006 A0 B20 L100 C37 D20 M30 E37 F20 N60\nM1006 W\n\nM17 X0.8 Y0.8 Z0.5 ; lower motor current to 45% power\nM960 S5 P0 ; turn off logo lamp\n
; machine_hotend_change_time = 0
; machine_load_filament_time = 29
; machine_max_acceleration_e = 5000,5000
; machine_max_acceleration_extruding = 20000,20000
; machine_max_acceleration_retracting = 5000,5000
; machine_max_acceleration_travel = 9000,9000
; machine_max_acceleration_x = 20000,20000
; machine_max_acceleration_y = 20000,20000
; machine_max_acceleration_z = 500,500
; machine_max_force_Y = 0
; machine_max_jerk_e = 2.5,2.5
; machine_max_jerk_x = 9,9
; machine_max_jerk_y = 9,9
; machine_max_jerk_z = 3,3
; machine_max_printed_mass = 0
; machine_max_speed_e = 30,30
; machine_max_speed_x = 500,500
; machine_max_speed_y = 500,500
; machine_max_speed_z = 20,20
; machine_min_extruding_rate = 0
; machine_min_travel_rate = 0
; machine_pause_gcode = M400 U1
; machine_prepare_compensation_time = 260
; machine_start_gcode = ;===== machine: X1-0.4 ====================\n;===== date: 20251031 ==================\n;===== start printer sound ================\nM17\nM400 S1\nM1006 S1\nM1006 A0 B10 L100 C37 D10 M60 E37 F10 N60\nM1006 A0 B10 L100 C41 D10 M60 E41 F10 N60\nM1006 A0 B10 L100 C44 D10 M60 E44 F10 N60\nM1006 A0 B10 L100 C0 D10 M60 E0 F10 N60\nM1006 A46 B10 L100 C43 D10 M70 E39 F10 N100\nM1006 A0 B10 L100 C0 D10 M60 E0 F10 N100\nM1006 A43 B10 L100 C0 D10 M60 E39 F10 N100\nM1006 A0 B10 L100 C0 D10 M60 E0 F10 N100\nM1006 A41 B10 L100 C0 D10 M100 E41 F10 N100\nM1006 A44 B10 L100 C0 D10 M100 E44 F10 N100\nM1006 A49 B10 L100 C0 D10 M100 E49 F10 N100\nM1006 A0 B10 L100 C0 D10 M100 E0 F10 N100\nM1006 A48 B10 L100 C44 D10 M60 E39 F10 N100\nM1006 A0 B10 L100 C0 D10 M60 E0 F10 N100\nM1006 A44 B10 L100 C0 D10 M90 E39 F10 N100\nM1006 A0 B10 L100 C0 D10 M60 E0 F10 N100\nM1006 A46 B10 L100 C43 D10 M60 E39 F10 N100\nM1006 W\n;===== turn on the HB fan =================\nM104 S75 ;set extruder temp to turn on the HB fan and prevent filament oozing from nozzle\n;===== reset machine status =================\nM290 X40 Y40 Z2.6666666\nG91\nM17 Z0.4 ; lower the z-motor current\nG380 S2 Z30 F300 ; G380 is same as G38; lower the hotbed , to prevent the nozzle is below the hotbed\nG380 S2 Z-25 F300 ;\nG1 Z5 F300;\nG90\nM17 X1.2 Y1.2 Z0.75 ; reset motor current to default\nM960 S5 P1 ; turn on logo lamp\nG90\nM220 S100 ;Reset Feedrate\nM221 S100 ;Reset Flowrate\nM73.2   R1.0 ;Reset left time magnitude\nM1002 set_gcode_claim_speed_level : 5\nM221 X0 Y0 Z0 ; turn off soft endstop to prevent protential logic problem\nG29.1 Z{+0.0} ; clear z-trim value first\nM204 S10000 ; init ACC set to 10m/s^2\n\n;===== heatbed preheat ====================\nM1002 gcode_claim_action:54\nM140 S[bed_temperature_initial_layer_single] ;set bed temp\nM190 S[bed_temperature_initial_layer_single] ;wait for bed temp\n\n{if scan_first_layer}\n;=========register first layer scan=====\nM977 S1 P60\n{endif}\n\n;=============turn on fans to prevent PLA jamming=================\n{if filament_type[initial_no_support_extruder]==\"PLA\"}\n    {if (bed_temperature[initial_no_support_extruder] >45)||(bed_temperature_initial_layer[initial_no_support_extruder] >45)}\n    M106 P3 S180\n    {endif};Prevent PLA from jamming\n    M142 P1 R35 S40\n{endif}\nM106 P2 S100 ; turn on big fan ,to cool down toolhead\n\n;===== prepare print temperature and material ==========\nM104 S[nozzle_temperature_initial_layer] ;set extruder temp\nG91\nG0 Z10 F1200\nG90\nG28 X\nM975 S1 ; turn on\nG1 X60 F12000\nG1 Y245\nG1 Y265 F3000\nM620 M\nM620 S[initial_no_support_extruder]A   ; switch material if AMS exist\n    M109 S[nozzle_temperature_initial_layer]\n    G1 X120 F12000\n\n    G1 X20 Y50 F12000\n    G1 Y-3\n    T[initial_no_support_extruder]\n    G1 X54 F12000\n    G1 Y265\n    M400\nM621 S[initial_no_support_extruder]A\nM620.1 E F{flush_volumetric_speeds[initial_no_support_extruder]/2.4053*60} T{flush_temperatures[initial_no_support_extruder]}\n\nM412 S1 ; ===turn on filament runout detection===\n\nM109 S250 ;set nozzle to common flush temp\nM106 P1 S0\nG92 E0\nG1 E50 F200\nM400\nM104 S[nozzle_temperature_initial_layer]\nG92 E0\nG1 E50 F200\nM400\nM106 P1 S255\nG92 E0\nG1 E5 F300\nM109 S{nozzle_temperature_initial_layer[initial_no_support_extruder]-20} ; drop nozzle temp, make filament shink a bit\nG92 E0\nG1 E-0.5 F300\n\nG1 X70 F9000\nG1 X76 F15000\nG1 X65 F15000\nG1 X76 F15000\nG1 X65 F15000; shake to put down garbage\nG1 X80 F6000\nG1 X95 F15000\nG1 X80 F15000\nG1 X165 F15000; wipe and shake\nM400\nM106 P1 S0\n;===== prepare print temperature and material end =====\n\n\n;===== wipe nozzle ===============================\nM1002 gcode_claim_action : 14\nM975 S1\nM106 S255\nG1 X65 Y230 F18000\nG1 Y264 F6000\nM109 S{nozzle_temperature_initial_layer[initial_no_support_extruder]-20}\nG1 X100 F18000 ; first wipe mouth\n\nG0 X135 Y253 F20000  ; move to exposed steel surface edge\nG28 Z P0 T300; home z with low precision,permit 300deg temperature\nG29.2 S0 ; turn off ABL\nG0 Z5 F20000\n\nG1 X60 Y265\nG92 E0\nG1 E-0.5 F300 ; retrack more\nG1 X100 F5000; second wipe mouth\nG1 X70 F15000\nG1 X100 F5000\nG1 X70 F15000\nG1 X100 F5000\nG1 X70 F15000\nG1 X100 F5000\nG1 X70 F15000\nG1 X90 F5000\nG0 X128 Y261 Z-1.5 F20000  ; move to exposed steel surface and stop the nozzle\nM104 S140 ; set temp down to heatbed acceptable\nM106 S255 ; turn on fan (G28 has turn off fan)\n\nM221 S; push soft endstop status\nM221 Z0 ;turn off Z axis endstop\nG0 Z0.5 F20000\nG0 X125 Y259.5 Z-1.01\nG0 X131 F211\nG0 X124\nG0 Z0.5 F20000\nG0 X125 Y262.5\nG0 Z-1.01\nG0 X131 F211\nG0 X124\nG0 Z0.5 F20000\nG0 X125 Y260.0\nG0 Z-1.01\nG0 X131 F211\nG0 X124\nG0 Z0.5 F20000\nG0 X125 Y262.0\nG0 Z-1.01\nG0 X131 F211\nG0 X124\nG0 Z0.5 F20000\nG0 X125 Y260.5\nG0 Z-1.01\nG0 X131 F211\nG0 X124\nG0 Z0.5 F20000\nG0 X125 Y261.5\nG0 Z-1.01\nG0 X131 F211\nG0 X124\nG0 Z0.5 F20000\nG0 X125 Y261.0\nG0 Z-1.01\nG0 X131 F211\nG0 X124\nG0 X128\nG2 I0.5 J0 F300\nG2 I0.5 J0 F300\nG2 I0.5 J0 F300\nG2 I0.5 J0 F300\n\nM109 S140 ; wait nozzle temp down to heatbed acceptable\nG2 I0.5 J0 F3000\nG2 I0.5 J0 F3000\nG2 I0.5 J0 F3000\nG2 I0.5 J0 F3000\n\nM221 R; pop softend status\nG1 Z10 F1200\nM400\nG1 Z10\nG1 F30000\nG1 X128 Y128\nG29.2 S1 ; turn on ABL\n;G28 ; home again after hard wipe mouth\nM106 S0 ; turn off fan , too noisy\n;===== wipe nozzle end ================================\n\n;===== check scanner clarity ===========================\nG1 X128 Y128 F24000\nG28 Z P0\nM972 S5 P0\nG1 X230 Y15 F24000\n;===== check scanner clarity end =======================\n\n;===== bed leveling ==================================\nM1002 judge_flag g29_before_print_flag\nM622 J1\n\n    M1002 gcode_claim_action : 1\n    G29 A X{first_layer_print_min[0]} Y{first_layer_print_min[1]} I{first_layer_print_size[0]} J{first_layer_print_size[1]}\n    M400\n    M500 ; save cali data\n\nM623\n;===== bed leveling end ================================\n\n;===== home after wipe mouth============================\nM1002 judge_flag g29_before_print_flag\nM622 J0\n\n    M1002 gcode_claim_action : 13\n    G28\n\nM623\n;===== home after wipe mouth end =======================\n\nM975 S1 ; turn on vibration supression\n\n;=============turn on fans to prevent PLA jamming=================\n{if filament_type[initial_no_support_extruder]==\"PLA\"}\n    {if (bed_temperature[initial_no_support_extruder] >45)||(bed_temperature_initial_layer[initial_no_support_extruder] >45)}\n    M106 P3 S180\n    {endif};Prevent PLA from jamming\n    M142 P1 R35 S40\n{endif}\nM106 P2 S100 ; turn on big fan ,to cool down toolhead\n\nM104 S{nozzle_temperature_initial_layer[initial_no_support_extruder]} ; set extrude temp earlier, to reduce wait time\n\n;===== mech mode fast check============================\nG1 X128 Y128 Z10 F20000\nM400 P200\nM970.3 Q1 A7 B30 C80  H15 K0\nM974 Q1 S2 P0\n\nG1 X128 Y128 Z10 F20000\nM400 P200\nM970.3 Q0 A7 B30 C90 Q0 H15 K0\nM974 Q0 S2 P0\n\nM975 S1\nG1 F30000\nG1 X230 Y15\nG28 X ; re-home XY\n;===== mech mode fast check============================\n\n{if scan_first_layer}\n;start heatbed  scan====================================\nM976 S2 P1\nG90\nG1 X128 Y128 F20000\nM976 S3 P2  ;register void printing detection\n{endif}\n\n;===== nozzle load line ===============================\nM975 S1\nG90\nM83\nT1000\nG1 X18.0 Y1.0 Z0.8 F18000;Move to start position\nM109 S{nozzle_temperature[initial_no_support_extruder]}\nG1 Z0.2\nG0 E2 F300\nG0 X240 E15 F{outer_wall_volumetric_speed/(0.3*0.5)     * 60}\nG0 Y11 E0.700 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\nG0 X239.5\nG0 E0.2\nG0 Y1.5 E0.700\nG0 X231 E0.700 F{outer_wall_volumetric_speed/(0.3*0.5)     * 60}\nM400\n\n;===== for Textured PEI Plate , lower the nozzle as the nozzle was touching topmost of the texture when homing ==\n;curr_bed_type={curr_bed_type}\n{if curr_bed_type==\"Textured PEI Plate\"}\nG29.1 Z{-0.04} ; for Textured PEI Plate\n{endif}\n\n;===== draw extrinsic para cali paint =================\nM1002 judge_flag extrude_cali_flag\nM622 J1\n\n    M1002 gcode_claim_action : 8\n\n    T1000\n\n    G0 F1200.0 X231 Y12   Z0.2 E0.577\n    G0 F1200.0 X226 Y12   Z0.2 E0.275\n    G0 F1200.0 X226 Y1.5  Z0.2 E0.577\n    G0 F1200.0 X220 Y1.5  Z0.2 E0.330\n    G0 F1200.0 X220 Y8    Z0.2 E0.358\n    G0 F1200.0 X210 Y8    Z0.2 E0.549\n    G0 F1200.0 X210 Y1.5  Z0.2 E0.357\n\n    G0 X48.0 E11.9 F{outer_wall_volumetric_speed/(0.3*0.5)     * 60}\n    G0 X48.0 Y12 E0.772 F1200.0\n    G0 X45.0 E0.22 F1200.0\n    G0 X35.0 Y6.0 E0.86 F1200.0\n\n    ;=========== extruder cali extrusion ==================\n    T1000\n    M83\n    {if default_acceleration > 0}\n        {if outer_wall_acceleration > 0}\n            M204 S[outer_wall_acceleration]\n        {else}\n            M204 S[default_acceleration]\n        {endif}\n    {endif}\n    G0 X35.000 Y6.000 Z0.300 F30000 E0\n    G1 F1500.000 E0.800\n    M106 S0 ; turn off fan\n    G0 X185.000 E9.35441 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G0 X187 Z0\n    G1 F1500.000 E-0.800\n    G0 Z1\n    G0 X180 Z0.3 F18000\n\n    M900 L1000.0 M1.0\n    M900 K0.040\n    G0 X45.000 F30000\n    G0 Y8.000 F30000\n    G1 F1500.000 E0.800\n    G1 X65.000 E1.24726 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X70.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X75.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X80.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X85.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X90.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X95.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X100.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X105.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X110.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X115.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X120.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X125.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X130.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X135.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X140.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X145.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X150.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X155.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X160.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X165.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X170.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X175.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X180.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 F1500.000 E-0.800\n    G1 X183 Z0.15 F30000\n    G1 X185\n    G1 Z1.0\n    G0 Y6.000 F30000 ; move y to clear pos\n    G1 Z0.3\n    M400\n\n    G0 X45.000 F30000\n    M900 K0.020\n    G0 X45.000 F30000\n    G0 Y10.000 F30000\n    G1 F1500.000 E0.800\n    G1 X65.000 E1.24726 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X70.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X75.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X80.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X85.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X90.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X95.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X100.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X105.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X110.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X115.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X120.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X125.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X130.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X135.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X140.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X145.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X150.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X155.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X160.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X165.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X170.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X175.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X180.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 F1500.000 E-0.800\n    G1 X183 Z0.15 F30000\n    G1 X185\n    G1 Z1.0\n    G0 Y6.000 F30000 ; move y to clear pos\n    G1 Z0.3\n    M400\n\n    G0 X45.000 F30000\n    M900 K0.000\n    G0 X45.000 F30000\n    G0 Y12.000 F30000\n    G1 F1500.000 E0.800\n    G1 X65.000 E1.24726 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X70.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X75.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X80.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X85.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X90.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X95.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X100.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X105.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X110.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X115.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X120.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X125.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X130.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X135.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X140.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X145.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X150.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X155.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X160.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X165.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X170.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X175.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X180.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 F1500.000 E-0.800\n    G1 X183 Z0.15 F30000\n    G1 X185\n    G1 Z1.0\n    G0 Y6.000 F30000 ; move y to clear pos\n    G1 Z0.3\n\n    G0 X45.000 F30000 ; move to start point\n\nM623 ; end of \"draw extrinsic para cali paint\"\n\n\nM1002 judge_flag extrude_cali_flag\nM622 J0\n    G0 X231 Y1.5 F30000\n    G0 X18 E14.3 F{outer_wall_volumetric_speed/(0.3*0.5)     * 60}\nM623\n\nM104 S140\n\n\n;=========== laser and rgb calibration ===========\nM400\nM18 E\nM500 R\n\nM973 S3 P14\n\nG1 X120 Y1.0 Z0.3 F18000.0;Move to first extrude line pos\nT1100\nG1 X235.0 Y1.0 Z0.3 F18000.0;Move to first extrude line pos\nM400 P100\nM960 S1 P1\nM400 P100\nM973 S6 P0; use auto exposure for horizontal laser by xcam\nM960 S0 P0\n\nG1 X240.0 Y6.0 Z0.3 F18000.0;Move to vertical extrude line pos\nM960 S2 P1\nM400 P100\nM973 S6 P1; use auto exposure for vertical laser by xcam\nM960 S0 P0\n\n;=========== handeye calibration ======================\nM1002 judge_flag extrude_cali_flag\nM622 J1\n\n    M973 S3 P1 ; camera start stream\n    M400 P500\n    M973 S1\n    G0 F6000 X228.500 Y4.750 Z0.000\n    M960 S0 P1\n    M973 S1\n    M400 P800\n    M971 S6 P0\n    M973 S2 P0\n    M400 P500\n    G0 Z0.000 F12000\n    M960 S0 P0\n    M960 S1 P1\n    G0 X215.00 Y4.750\n    M400 P200\n    M971 S5 P1\n    M973 S2 P1\n    M400 P500\n    M960 S0 P0\n    M960 S2 P1\n    G0 X228.5 Y6.75\n    M400 P200\n    M971 S5 P3\n    G0 Z0.500 F12000\n    M960 S0 P0\n    M960 S2 P1\n    G0 X228.5 Y6.75\n    M400 P200\n    M971 S5 P4\n    M973 S2 P0\n    M400 P500\n    M960 S0 P0\n    M960 S1 P1\n    G0 X215.00 Y4.750\n    M400 P500\n    M971 S5 P2\n    M963 S1\n    M400 P1500\n    M964\n    T1100\n    G1 Z3 F3000\n\n    M400\n    M500 ; save cali data\n\n    M104 S{nozzle_temperature[initial_no_support_extruder]} ; rise nozzle temp now ,to reduce temp waiting time.\n\n    T1100\n    M400 P400\n    M960 S0 P0\n    G0 F30000.000 Y10.000 X65.000 Z0.000\n    M400 P400\n    M960 S1 P1\n    M400 P50\n\n    M969 S1 N3 A2000\n    G0 F360.000 X181.000 Z0.000\n    M980.3 A70.000 B{outer_wall_volumetric_speed/(1.75*1.75/4*3.14)*60/4} C5.000 D{outer_wall_volumetric_speed/(1.75*1.75/4*3.14)*60} E5.000 F175.000 H1.000 I0.000 J0.020 K0.040\n    M400 P100\n    G0 F20000\n    G0 Z1 ; rise nozzle up\n    T1000 ; change to nozzle space\n    G0 X45.000 Y4.000 F30000 ; move to test line pos\n    M969 S0 ; turn off scanning\n    M960 S0 P0\n\n\n    G1 Z2 F20000\n    T1000\n    G0 X45.000 Y4.000 F30000 E0\n    M109 S{nozzle_temperature[initial_no_support_extruder]}\n    G0 Z0.3\n    G1 F1500.000 E3.600\n    G1 X65.000 E1.24726 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X70.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X75.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X80.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X85.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X90.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X95.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X100.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X105.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X110.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X115.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X120.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X125.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X130.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X135.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n\n    ; see if extrude cali success, if not ,use default value\n    M1002 judge_last_extrude_cali_success\n    M622 J0\n        M400\n        M900 K0.02 M{outer_wall_volumetric_speed/(1.75*1.75/4*3.14)*0.02}\n    M623\n\n    G1 X140.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X145.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X150.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X155.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X160.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X165.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X170.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X175.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X180.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X185.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X190.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X195.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X200.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X205.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X210.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X215.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    G1 X220.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)/ 4 * 60}\n    G1 X225.000 E0.31181 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\n    M973 S4\n\nM623\n\n;========turn off light and wait extrude temperature =============\nM1002 gcode_claim_action : 0\nM973 S4 ; turn off scanner\nM400 ; wait all motion done before implement the emprical L parameters\n;M900 L500.0 ; Empirical parameters\nM109 S[nozzle_temperature_initial_layer]\nM960 S1 P0 ; turn off laser\nM960 S2 P0 ; turn off laser\nM106 S0 ; turn off fan\nM106 P2 S0 ; turn off big fan\nM106 P3 S0 ; turn off chamber fan\n\nM975 S1 ; turn on mech mode supression\nG90\nM83\nT1000\n;===== purge line to wipe the nozzle ============================\nG1 E{-retraction_length[initial_no_support_extruder]} F1800\nG1 X18.0 Y2.5 Z0.8 F18000.0;Move to start position\nG1 E{retraction_length[initial_no_support_extruder]} F1800\nM109 S{nozzle_temperature_initial_layer[initial_no_support_extruder]}\nG1 Z0.2\nG0 X239 E15 F{outer_wall_volumetric_speed/(0.3*0.5)    * 60}\nG0 Y12 E0.7 F{outer_wall_volumetric_speed/(0.3*0.5)/4* 60}\n
; machine_switch_extruder_time = 0
; machine_unload_filament_time = 28
; master_extruder_id = 1
; max_bridge_length = 0
; max_layer_height = 0.28
; max_travel_detour_distance = 0
; min_bead_width = 85%
; min_feature_size = 25%
; min_layer_height = 0.08
; minimum_sparse_infill_area = 15
; mmu_segmented_region_interlocking_depth = 0
; mmu_segmented_region_max_width = 0
; monotonic_travel_into_wall = 0%
; no_slow_down_for_cooling_on_outwalls = 0
; nozzle_diameter = 0.4
; nozzle_flush_dataset = 0
; nozzle_height = 4.2
; nozzle_temperature = 220
; nozzle_temperature_initial_layer = 220
; nozzle_temperature_range_high = 240
; nozzle_temperature_range_low = 190
; nozzle_type = hardened_steel
; nozzle_volume = 107
; nozzle_volume_type = Standard
; only_one_wall_first_layer = 0
; ooze_prevention = 0
; other_layers_print_sequence = 0
; other_layers_print_sequence_nums = 0
; outer_wall_acceleration = 5000
; outer_wall_jerk = 9
; outer_wall_line_width = 0.42
; outer_wall_speed = 200
; overhang_1_4_speed = 0
; overhang_2_4_speed = 50
; overhang_3_4_speed = 30
; overhang_4_4_speed = 10
; overhang_fan_speed = 100
; overhang_fan_threshold = 50%
; overhang_threshold_participating_cooling = 95%
; overhang_totally_speed = 10
; override_filament_scarf_seam_setting = 0
; override_process_overhang_speed = 0
; physical_extruder_map = 0
; post_process = 
; pre_start_fan_time = 0
; precise_outer_wall = 0
; precise_z_height = 0
; pressure_advance = 0.02
; prime_tower_brim_width = 3
; prime_tower_enable_framework = 0
; prime_tower_extra_rib_length = 0
; prime_tower_fillet_wall = 1
; prime_tower_flat_ironing = 0
; prime_tower_infill_gap = 150%
; prime_tower_lift_height = -1
; prime_tower_lift_speed = 90
; prime_tower_max_speed = 90
; prime_tower_rib_wall = 1
; prime_tower_rib_width = 8
; prime_tower_skip_points = 1
; prime_tower_width = 35
; prime_volume_mode = Default
; print_compatible_printers = "Bambu Lab X1 Carbon 0.4 nozzle";"Bambu Lab X1 0.4 nozzle";"Bambu Lab P1S 0.4 nozzle";"Bambu Lab X1E 0.4 nozzle"
; print_extruder_id = 1
; print_extruder_variant = "Direct Drive Standard"
; print_flow_ratio = 1
; print_in_clockwise = 0
; print_sequence = by layer
; print_settings_id = 0.20mm Standard @BBL X1C
; printable_area = 0x0,256x0,256x256,0x256
; printable_height = 250
; printer_extruder_id = 1
; printer_extruder_variant = "Direct Drive Standard"
; printer_model = Bambu Lab X1 Carbon
; printer_notes = 
; printer_settings_id = Bambu Lab X1 Carbon 0.4 nozzle
; printer_structure = corexy
; printer_technology = FFF
; printer_variant = 0.4
; printing_by_object_gcode = 
; process_notes = 
; raft_contact_distance = 0.1
; raft_expansion = 1.5
; raft_first_layer_density = 90%
; raft_first_layer_expansion = -1
; raft_layers = 0
; reduce_crossing_wall = 0
; reduce_fan_stop_start_freq = 1
; reduce_infill_retraction_mode = Auto
; required_nozzle_HRC = 3
; resolution = 0.012
; retract_before_wipe = 0%
; retract_length_toolchange = 2
; retract_lift_above = 0
; retract_lift_below = 249
; retract_restart_extra = 0
; retract_restart_extra_toolchange = 0
; retract_when_changing_layer = 1
; retraction_distances_when_cut = 18
; retraction_distances_when_ec = 0
; retraction_length = 0.8
; retraction_minimum_travel = 1
; retraction_speed = 30
; role_base_wipe_speed = 1
; scan_first_layer = 1
; scarf_angle_threshold = 155
; seam_gap = 15%
; seam_placement_away_from_overhangs = 0
; seam_position = aligned
; seam_slope_conditional = 1
; seam_slope_entire_loop = 0
; seam_slope_gap = 0
; seam_slope_inner_walls = 1
; seam_slope_min_length = 10
; seam_slope_start_height = 10%
; seam_slope_steps = 10
; seam_slope_type = none
; silent_mode = 0
; single_extruder_multi_material = 1
; skeleton_infill_density = 15%
; skeleton_infill_line_width = 0.45
; skin_infill_density = 15%
; skin_infill_depth = 2
; skin_infill_line_width = 0.45
; skirt_distance = 2
; skirt_height = 1
; skirt_loops = 0
; skirt_per_object = 1
; slice_closing_radius = 0.049
; slicing_mode = regular
; slow_down_for_layer_cooling = 1
; slow_down_layer_time = 4
; slow_down_min_speed = 20
; slowdown_end_acc = 100000
; slowdown_end_height = 400
; slowdown_end_speed = 1000
; slowdown_start_acc = 100000
; slowdown_start_height = 0
; slowdown_start_speed = 1000
; small_perimeter_speed = 50%
; small_perimeter_threshold = 0
; smooth_coefficient = 150
; smooth_speed_discontinuity_area = 1
; solid_infill_filament = 0
; sparse_infill_acceleration = 100%
; sparse_infill_anchor = 400%
; sparse_infill_anchor_max = 20
; sparse_infill_density = 15%
; sparse_infill_filament = 0
; sparse_infill_lattice_angle_1 = -45
; sparse_infill_lattice_angle_2 = 45
; sparse_infill_line_width = 0.45
; sparse_infill_pattern = grid
; sparse_infill_speed = 270
; spiral_mode = 0
; spiral_mode_max_xy_smoothing = 200%
; spiral_mode_smooth = 0
; standby_temperature_delta = -5
; start_end_points = 30x-3,54x245
; supertack_plate_temp = 45
; supertack_plate_temp_initial_layer = 45
; support_air_filtration = 0
; support_angle = 0
; support_base_pattern = default
; support_base_pattern_spacing = 2.5
; support_bottom_interface_spacing = 0.5
; support_bottom_z_distance = 0.2
; support_chamber_temp_control = 0
; support_cooling_filter = 0
; support_critical_regions_only = 0
; support_expansion = 0
; support_fast_purge_mode = 0
; support_filament = 0
; support_interface_bottom_layers = 2
; support_interface_filament = 0
; support_interface_loop_pattern = 0
; support_interface_not_for_body = 1
; support_interface_pattern = auto
; support_interface_spacing = 0.5
; support_interface_speed = 80
; support_interface_top_layers = 2
; support_ironing_direction = 0
; support_ironing_flow = 10%
; support_ironing_inset = 0
; support_ironing_pattern = zig-zag
; support_ironing_spacing = 0.15
; support_ironing_speed = 30
; support_line_width = 0.42
; support_object_first_layer_gap = 0.2
; support_object_skip_flush = 0
; support_object_xy_distance = 0.35
; support_on_build_plate_only = 0
; support_remove_small_overhang = 1
; support_speed = 150
; support_style = default
; support_threshold_angle = 30
; support_top_z_distance = 0.2
; support_type = tree(auto)
; symmetric_infill_y_axis = 0
; temperature_vitrification = 45
; template_custom_gcode = 
; textured_plate_temp = 55
; textured_plate_temp_initial_layer = 55
; thick_bridges = 0
; thumbnail_size = 50x50
; time_lapse_gcode = ;========Date 20250206========\n; SKIPPABLE_START\n; SKIPTYPE: timelapse\nM622.1 S1 ; for prev firmware, default turned on\nM1002 judge_flag timelapse_record_flag\nM622 J1\n{if timelapse_type == 0} ; timelapse without wipe tower\nM971 S11 C10 O0\nM1004 S5 P1  ; external shutter\n{elsif timelapse_type == 1} ; timelapse with wipe tower\nG92 E0\nG1 X65 Y245 F20000 ; move to safe pos\nG17\nG2 Z{layer_z} I0.86 J0.86 P1 F20000\nG1 Y265 F3000\nM400\nM1004 S5 P1  ; external shutter\nM400 P300\nM971 S11 C10 O0\nG92 E0\nG1 X100 F5000\nG1 Y255 F20000\n{endif}\nM623\n; SKIPPABLE_END\n
; timelapse_type = 0
; top_area_threshold = 200%
; top_color_penetration_layers = 5
; top_one_wall_type = all top
; top_shell_layers = 5
; top_shell_thickness = 1
; top_solid_infill_flow_ratio = 1
; top_surface_acceleration = 2000
; top_surface_density = 100%
; top_surface_jerk = 9
; top_surface_line_width = 0.42
; top_surface_pattern = monotonicline
; top_surface_speed = 200
; top_z_overrides_xy_distance = 0
; travel_acceleration = 10000
; travel_jerk = 9
; travel_short_distance_acceleration = 250
; travel_speed = 500
; travel_speed_z = 0
; tree_support_branch_angle = 45
; tree_support_branch_diameter = 2
; tree_support_branch_diameter_angle = 5
; tree_support_branch_distance = 5
; tree_support_wall_count = -1
; upward_compatible_machine = "Bambu Lab P1S 0.4 nozzle";"Bambu Lab P1P 0.4 nozzle";"Bambu Lab X1 0.4 nozzle";"Bambu Lab X1E 0.4 nozzle";"Bambu Lab A1 0.4 nozzle";"Bambu Lab H2D 0.4 nozzle";"Bambu Lab H2D Pro 0.4 nozzle";"Bambu Lab H2S 0.4 nozzle";"Bambu Lab P2S 0.4 nozzle";"Bambu Lab H2C 0.4 nozzle";"Bambu Lab X2D 0.4 nozzle";"Bambu Lab A2L 0.4 nozzle"
; use_firmware_retraction = 0
; use_relative_e_distances = 1
; vertical_shell_speed = 80%
; volumetric_speed_coefficients = "0 0 0 0 0 0"
; wall_distribution_count = 1
; wall_filament = 0
; wall_generator = classic
; wall_loops = 2
; wall_sequence = inner wall/outer wall
; wall_transition_angle = 10
; wall_transition_filter_deviation = 25%
; wall_transition_length = 100%
; wipe = 1
; wipe_distance = 2
; wipe_speed = 80%
; wipe_tower_no_sparse_layers = 0
; wipe_tower_rotation_angle = 0
; wipe_tower_x = 15
; wipe_tower_y = 220
; wrapping_detection_gcode = 
; wrapping_detection_layers = 20
; wrapping_exclude_area = 
; xy_contour_compensation = 0
; xy_hole_compensation = 0
; z_direction_outwall_speed_continuous = 0
; z_hop = 0.4
; z_hop_types = Auto Lift
; CONFIG_BLOCK_END

; EXECUTABLE_BLOCK_START
M73 P0 R15
M201 X20000 Y20000 Z500 E5000
M203 X500 Y500 Z20 E30
M204 P20000 R5000 T20000
M205 X9.00 Y9.00 Z3.00 E2.50
M106 S0
M106 P2 S0
; FEATURE: Custom
;===== machine: X1-0.4 ====================
;===== date: 20251031 ==================
;===== start printer sound ================
M17
M400 S1
M1006 S1
M1006 A0 B10 L100 C37 D10 M60 E37 F10 N60
M1006 A0 B10 L100 C41 D10 M60 E41 F10 N60
M1006 A0 B10 L100 C44 D10 M60 E44 F10 N60
M1006 A0 B10 L100 C0 D10 M60 E0 F10 N60
M1006 A46 B10 L100 C43 D10 M70 E39 F10 N100
M1006 A0 B10 L100 C0 D10 M60 E0 F10 N100
M1006 A43 B10 L100 C0 D10 M60 E39 F10 N100
M1006 A0 B10 L100 C0 D10 M60 E0 F10 N100
M1006 A41 B10 L100 C0 D10 M100 E41 F10 N100
M1006 A44 B10 L100 C0 D10 M100 E44 F10 N100
M1006 A49 B10 L100 C0 D10 M100 E49 F10 N100
M1006 A0 B10 L100 C0 D10 M100 E0 F10 N100
M1006 A48 B10 L100 C44 D10 M60 E39 F10 N100
M1006 A0 B10 L100 C0 D10 M60 E0 F10 N100
M1006 A44 B10 L100 C0 D10 M90 E39 F10 N100
M1006 A0 B10 L100 C0 D10 M60 E0 F10 N100
M1006 A46 B10 L100 C43 D10 M60 E39 F10 N100
M1006 W
;===== turn on the HB fan =================
M104 S75 ;set extruder temp to turn on the HB fan and prevent filament oozing from nozzle
;===== reset machine status =================
M290 X40 Y40 Z2.6666666
G91
M17 Z0.4 ; lower the z-motor current
G380 S2 Z30 F300 ; G380 is same as G38; lower the hotbed , to prevent the nozzle is below the hotbed
G380 S2 Z-25 F300 ;
G1 Z5 F300;
G90
M17 X1.2 Y1.2 Z0.75 ; reset motor current to default
M960 S5 P1 ; turn on logo lamp
G90
M220 S100 ;Reset Feedrate
M221 S100 ;Reset Flowrate
M73.2   R1.0 ;Reset left time magnitude
M1002 set_gcode_claim_speed_level : 5
M221 X0 Y0 Z0 ; turn off soft endstop to prevent protential logic problem
G29.1 Z0 ; clear z-trim value first
M204 S10000 ; init ACC set to 10m/s^2

;===== heatbed preheat ====================
M1002 gcode_claim_action:54
M140 S35 ;set bed temp
M190 S35 ;wait for bed temp


;=========register first layer scan=====
M977 S1 P60


;=============turn on fans to prevent PLA jamming=================

    ;Prevent PLA from jamming
    M142 P1 R35 S40

M106 P2 S100 ; turn on big fan ,to cool down toolhead

;===== prepare print temperature and material ==========
M104 S220 ;set extruder temp
G91
G0 Z10 F1200
G90
G28 X
M975 S1 ; turn on
G1 X60 F12000
G1 Y245
G1 Y265 F3000
M620 M
M620 S0A   ; switch material if AMS exist
    M109 S220
    G1 X120 F12000

    G1 X20 Y50 F12000
    G1 Y-3
    T0
    G1 X54 F12000
    G1 Y265
    M400
M621 S0A
M620.1 E F523.843 T240

M412 S1 ; ===turn on filament runout detection===

M109 S250 ;set nozzle to common flush temp
M106 P1 S0
G92 E0
M73 P3 R15
G1 E50 F200
M400
M104 S220
G92 E0
M73 P31 R10
G1 E50 F200
M400
M106 P1 S255
G92 E0
G1 E5 F300
M109 S200 ; drop nozzle temp, make filament shink a bit
G92 E0
M73 P32 R10
G1 E-0.5 F300

M73 P34 R10
G1 X70 F9000
G1 X76 F15000
G1 X65 F15000
G1 X76 F15000
G1 X65 F15000; shake to put down garbage
G1 X80 F6000
G1 X95 F15000
G1 X80 F15000
G1 X165 F15000; wipe and shake
M400
M106 P1 S0
;===== prepare print temperature and material end =====


;===== wipe nozzle ===============================
M1002 gcode_claim_action : 14
M975 S1
M106 S255
G1 X65 Y230 F18000
G1 Y264 F6000
M109 S200
G1 X100 F18000 ; first wipe mouth

G0 X135 Y253 F20000  ; move to exposed steel surface edge
G28 Z P0 T300; home z with low precision,permit 300deg temperature
G29.2 S0 ; turn off ABL
G0 Z5 F20000

G1 X60 Y265
G92 E0
G1 E-0.5 F300 ; retrack more
G1 X100 F5000; second wipe mouth
G1 X70 F15000
G1 X100 F5000
G1 X70 F15000
G1 X100 F5000
G1 X70 F15000
G1 X100 F5000
G1 X70 F15000
G1 X90 F5000
G0 X128 Y261 Z-1.5 F20000  ; move to exposed steel surface and stop the nozzle
M104 S140 ; set temp down to heatbed acceptable
M106 S255 ; turn on fan (G28 has turn off fan)

M221 S; push soft endstop status
M221 Z0 ;turn off Z axis endstop
G0 Z0.5 F20000
G0 X125 Y259.5 Z-1.01
G0 X131 F211
G0 X124
G0 Z0.5 F20000
G0 X125 Y262.5
G0 Z-1.01
G0 X131 F211
G0 X124
G0 Z0.5 F20000
G0 X125 Y260.0
G0 Z-1.01
G0 X131 F211
G0 X124
G0 Z0.5 F20000
G0 X125 Y262.0
G0 Z-1.01
G0 X131 F211
G0 X124
G0 Z0.5 F20000
G0 X125 Y260.5
G0 Z-1.01
G0 X131 F211
G0 X124
G0 Z0.5 F20000
G0 X125 Y261.5
G0 Z-1.01
G0 X131 F211
G0 X124
G0 Z0.5 F20000
G0 X125 Y261.0
G0 Z-1.01
G0 X131 F211
G0 X124
G0 X128
G2 I0.5 J0 F300
G2 I0.5 J0 F300
G2 I0.5 J0 F300
M73 P35 R10
G2 I0.5 J0 F300

M109 S140 ; wait nozzle temp down to heatbed acceptable
G2 I0.5 J0 F3000
G2 I0.5 J0 F3000
G2 I0.5 J0 F3000
G2 I0.5 J0 F3000

M221 R; pop softend status
G1 Z10 F1200
M400
G1 Z10
G1 F30000
G1 X128 Y128
G29.2 S1 ; turn on ABL
;G28 ; home again after hard wipe mouth
M106 S0 ; turn off fan , too noisy
;===== wipe nozzle end ================================

;===== check scanner clarity ===========================
G1 X128 Y128 F24000
G28 Z P0
M972 S5 P0
G1 X230 Y15 F24000
;===== check scanner clarity end =======================

;===== bed leveling ==================================
M1002 judge_flag g29_before_print_flag
M622 J1

    M1002 gcode_claim_action : 1
    G29 A X118 Y118 I20 J20
    M400
    M500 ; save cali data

M623
;===== bed leveling end ================================

;===== home after wipe mouth============================
M1002 judge_flag g29_before_print_flag
M622 J0

    M1002 gcode_claim_action : 13
    G28

M623
;===== home after wipe mouth end =======================

M975 S1 ; turn on vibration supression

;=============turn on fans to prevent PLA jamming=================

    ;Prevent PLA from jamming
    M142 P1 R35 S40

M106 P2 S100 ; turn on big fan ,to cool down toolhead

M104 S220 ; set extrude temp earlier, to reduce wait time

;===== mech mode fast check============================
G1 X128 Y128 Z10 F20000
M400 P200
M970.3 Q1 A7 B30 C80  H15 K0
M974 Q1 S2 P0

G1 X128 Y128 Z10 F20000
M400 P200
M970.3 Q0 A7 B30 C90 Q0 H15 K0
M974 Q0 S2 P0

M975 S1
G1 F30000
G1 X230 Y15
G28 X ; re-home XY
;===== mech mode fast check============================


;start heatbed  scan====================================
M976 S2 P1
G90
G1 X128 Y128 F20000
M976 S3 P2  ;register void printing detection


;===== nozzle load line ===============================
M975 S1
G90
M83
T1000
M73 P36 R10
G1 X18.0 Y1.0 Z0.8 F18000;Move to start position
M109 S220
G1 Z0.2
G0 E2 F300
G0 X240 E15 F6033.27
G0 Y11 E0.700 F1508.32
G0 X239.5
G0 E0.2
G0 Y1.5 E0.700
G0 X231 E0.700 F6033.27
M400

;===== for Textured PEI Plate , lower the nozzle as the nozzle was touching topmost of the texture when homing ==
;curr_bed_type=Cool Plate


;===== draw extrinsic para cali paint =================
M1002 judge_flag extrude_cali_flag
M622 J1

    M1002 gcode_claim_action : 8

    T1000

    G0 F1200.0 X231 Y12   Z0.2 E0.577
    G0 F1200.0 X226 Y12   Z0.2 E0.275
    G0 F1200.0 X226 Y1.5  Z0.2 E0.577
    G0 F1200.0 X220 Y1.5  Z0.2 E0.330
    G0 F1200.0 X220 Y8    Z0.2 E0.358
    G0 F1200.0 X210 Y8    Z0.2 E0.549
    G0 F1200.0 X210 Y1.5  Z0.2 E0.357

    G0 X48.0 E11.9 F6033.27
    G0 X48.0 Y12 E0.772 F1200.0
    G0 X45.0 E0.22 F1200.0
    G0 X35.0 Y6.0 E0.86 F1200.0

    ;=========== extruder cali extrusion ==================
    T1000
    M83
    
        
            M204 S5000
        
    
    G0 X35.000 Y6.000 Z0.300 F30000 E0
    G1 F1500.000 E0.800
    M106 S0 ; turn off fan
    G0 X185.000 E9.35441 F6033.27
    G0 X187 Z0
    G1 F1500.000 E-0.800
    G0 Z1
    G0 X180 Z0.3 F18000

    M900 L1000.0 M1.0
    M900 K0.040
    G0 X45.000 F30000
    G0 Y8.000 F30000
    G1 F1500.000 E0.800
    G1 X65.000 E1.24726 F1508.32
    G1 X70.000 E0.31181 F1508.32
    G1 X75.000 E0.31181 F6033.27
    G1 X80.000 E0.31181 F1508.32
    G1 X85.000 E0.31181 F6033.27
    G1 X90.000 E0.31181 F1508.32
M73 P37 R9
    G1 X95.000 E0.31181 F6033.27
    G1 X100.000 E0.31181 F1508.32
    G1 X105.000 E0.31181 F6033.27
    G1 X110.000 E0.31181 F1508.32
    G1 X115.000 E0.31181 F6033.27
    G1 X120.000 E0.31181 F1508.32
    G1 X125.000 E0.31181 F6033.27
    G1 X130.000 E0.31181 F1508.32
    G1 X135.000 E0.31181 F6033.27
    G1 X140.000 E0.31181 F1508.32
    G1 X145.000 E0.31181 F6033.27
M73 P38 R9
    G1 X150.000 E0.31181 F1508.32
    G1 X155.000 E0.31181 F6033.27
    G1 X160.000 E0.31181 F1508.32
    G1 X165.000 E0.31181 F6033.27
    G1 X170.000 E0.31181 F1508.32
    G1 X175.000 E0.31181 F6033.27
    G1 X180.000 E0.31181 F6033.27
    G1 F1500.000 E-0.800
    G1 X183 Z0.15 F30000
    G1 X185
    G1 Z1.0
    G0 Y6.000 F30000 ; move y to clear pos
    G1 Z0.3
    M400

    G0 X45.000 F30000
    M900 K0.020
    G0 X45.000 F30000
    G0 Y10.000 F30000
    G1 F1500.000 E0.800
    G1 X65.000 E1.24726 F1508.32
    G1 X70.000 E0.31181 F1508.32
    G1 X75.000 E0.31181 F6033.27
    G1 X80.000 E0.31181 F1508.32
    G1 X85.000 E0.31181 F6033.27
    G1 X90.000 E0.31181 F1508.32
    G1 X95.000 E0.31181 F6033.27
    G1 X100.000 E0.31181 F1508.32
    G1 X105.000 E0.31181 F6033.27
    G1 X110.000 E0.31181 F1508.32
    G1 X115.000 E0.31181 F6033.27
    G1 X120.000 E0.31181 F1508.32
M73 P39 R9
    G1 X125.000 E0.31181 F6033.27
    G1 X130.000 E0.31181 F1508.32
    G1 X135.000 E0.31181 F6033.27
    G1 X140.000 E0.31181 F1508.32
    G1 X145.000 E0.31181 F6033.27
    G1 X150.000 E0.31181 F1508.32
    G1 X155.000 E0.31181 F6033.27
    G1 X160.000 E0.31181 F1508.32
    G1 X165.000 E0.31181 F6033.27
    G1 X170.000 E0.31181 F1508.32
    G1 X175.000 E0.31181 F6033.27
    G1 X180.000 E0.31181 F6033.27
    G1 F1500.000 E-0.800
    G1 X183 Z0.15 F30000
    G1 X185
    G1 Z1.0
    G0 Y6.000 F30000 ; move y to clear pos
    G1 Z0.3
    M400

    G0 X45.000 F30000
    M900 K0.000
    G0 X45.000 F30000
    G0 Y12.000 F30000
    G1 F1500.000 E0.800
    G1 X65.000 E1.24726 F1508.32
    G1 X70.000 E0.31181 F1508.32
M73 P40 R9
    G1 X75.000 E0.31181 F6033.27
    G1 X80.000 E0.31181 F1508.32
    G1 X85.000 E0.31181 F6033.27
    G1 X90.000 E0.31181 F1508.32
    G1 X95.000 E0.31181 F6033.27
    G1 X100.000 E0.31181 F1508.32
    G1 X105.000 E0.31181 F6033.27
    G1 X110.000 E0.31181 F1508.32
    G1 X115.000 E0.31181 F6033.27
    G1 X120.000 E0.31181 F1508.32
    G1 X125.000 E0.31181 F6033.27
    G1 X130.000 E0.31181 F1508.32
    G1 X135.000 E0.31181 F6033.27
    G1 X140.000 E0.31181 F1508.32
    G1 X145.000 E0.31181 F6033.27
    G1 X150.000 E0.31181 F1508.32
    G1 X155.000 E0.31181 F6033.27
    G1 X160.000 E0.31181 F1508.32
    G1 X165.000 E0.31181 F6033.27
    G1 X170.000 E0.31181 F1508.32
    G1 X175.000 E0.31181 F6033.27
    G1 X180.000 E0.31181 F6033.27
    G1 F1500.000 E-0.800
    G1 X183 Z0.15 F30000
    G1 X185
    G1 Z1.0
    G0 Y6.000 F30000 ; move y to clear pos
    G1 Z0.3

    G0 X45.000 F30000 ; move to start point

M623 ; end of "draw extrinsic para cali paint"


M1002 judge_flag extrude_cali_flag
M622 J0
    G0 X231 Y1.5 F30000
    G0 X18 E14.3 F6033.27
M623

M104 S140


;=========== laser and rgb calibration ===========
M400
M18 E
M500 R

M973 S3 P14

G1 X120 Y1.0 Z0.3 F18000.0;Move to first extrude line pos
T1100
G1 X235.0 Y1.0 Z0.3 F18000.0;Move to first extrude line pos
M400 P100
M960 S1 P1
M400 P100
M973 S6 P0; use auto exposure for horizontal laser by xcam
M960 S0 P0

G1 X240.0 Y6.0 Z0.3 F18000.0;Move to vertical extrude line pos
M960 S2 P1
M400 P100
M973 S6 P1; use auto exposure for vertical laser by xcam
M960 S0 P0

;=========== handeye calibration ======================
M1002 judge_flag extrude_cali_flag
M622 J1

    M973 S3 P1 ; camera start stream
    M400 P500
    M973 S1
    G0 F6000 X228.500 Y4.750 Z0.000
    M960 S0 P1
    M973 S1
    M400 P800
    M971 S6 P0
    M973 S2 P0
    M400 P500
    G0 Z0.000 F12000
    M960 S0 P0
    M960 S1 P1
    G0 X215.00 Y4.750
    M400 P200
    M971 S5 P1
    M973 S2 P1
    M400 P500
    M960 S0 P0
    M960 S2 P1
    G0 X228.5 Y6.75
    M400 P200
    M971 S5 P3
    G0 Z0.500 F12000
    M960 S0 P0
    M960 S2 P1
    G0 X228.5 Y6.75
    M400 P200
    M971 S5 P4
    M973 S2 P0
    M400 P500
    M960 S0 P0
    M960 S1 P1
    G0 X215.00 Y4.750
    M400 P500
    M971 S5 P2
    M963 S1
    M400 P1500
    M964
    T1100
    G1 Z3 F3000

    M400
    M500 ; save cali data

    M104 S220 ; rise nozzle temp now ,to reduce temp waiting time.

    T1100
    M400 P400
    M960 S0 P0
    G0 F30000.000 Y10.000 X65.000 Z0.000
    M400 P400
    M960 S1 P1
    M400 P50

    M969 S1 N3 A2000
    G0 F360.000 X181.000 Z0.000
    M980.3 A70.000 B94.1106 C5.000 D376.442 E5.000 F175.000 H1.000 I0.000 J0.020 K0.040
    M400 P100
    G0 F20000
    G0 Z1 ; rise nozzle up
    T1000 ; change to nozzle space
    G0 X45.000 Y4.000 F30000 ; move to test line pos
    M969 S0 ; turn off scanning
    M960 S0 P0


    G1 Z2 F20000
    T1000
    G0 X45.000 Y4.000 F30000 E0
    M109 S220
    G0 Z0.3
    G1 F1500.000 E3.600
    G1 X65.000 E1.24726 F1508.32
    G1 X70.000 E0.31181 F1508.32
    G1 X75.000 E0.31181 F6033.27
    G1 X80.000 E0.31181 F1508.32
    G1 X85.000 E0.31181 F6033.27
    G1 X90.000 E0.31181 F1508.32
    G1 X95.000 E0.31181 F6033.27
    G1 X100.000 E0.31181 F1508.32
    G1 X105.000 E0.31181 F6033.27
    G1 X110.000 E0.31181 F1508.32
    G1 X115.000 E0.31181 F6033.27
    G1 X120.000 E0.31181 F1508.32
    G1 X125.000 E0.31181 F6033.27
    G1 X130.000 E0.31181 F1508.32
    G1 X135.000 E0.31181 F6033.27

    ; see if extrude cali success, if not ,use default value
    M1002 judge_last_extrude_cali_success
    M622 J0
        M400
        M900 K0.02 M0.125481
    M623

    G1 X140.000 E0.31181 F1508.32
    G1 X145.000 E0.31181 F6033.27
    G1 X150.000 E0.31181 F1508.32
    G1 X155.000 E0.31181 F6033.27
    G1 X160.000 E0.31181 F1508.32
    G1 X165.000 E0.31181 F6033.27
    G1 X170.000 E0.31181 F1508.32
    G1 X175.000 E0.31181 F6033.27
    G1 X180.000 E0.31181 F1508.32
    G1 X185.000 E0.31181 F6033.27
    G1 X190.000 E0.31181 F1508.32
    G1 X195.000 E0.31181 F6033.27
    G1 X200.000 E0.31181 F1508.32
    G1 X205.000 E0.31181 F6033.27
    G1 X210.000 E0.31181 F1508.32
M73 P41 R9
    G1 X215.000 E0.31181 F6033.27
    G1 X220.000 E0.31181 F1508.32
    G1 X225.000 E0.31181 F6033.27
    M973 S4

M623

;========turn off light and wait extrude temperature =============
M1002 gcode_claim_action : 0
M973 S4 ; turn off scanner
M400 ; wait all motion done before implement the emprical L parameters
;M900 L500.0 ; Empirical parameters
M109 S220
M960 S1 P0 ; turn off laser
M960 S2 P0 ; turn off laser
M106 S0 ; turn off fan
M106 P2 S0 ; turn off big fan
M106 P3 S0 ; turn off chamber fan

M975 S1 ; turn on mech mode supression
G90
M83
T1000
;===== purge line to wipe the nozzle ============================
G1 E-0.8 F1800
G1 X18.0 Y2.5 Z0.8 F18000.0;Move to start position
G1 E0.8 F1800
M109 S220
G1 Z0.2
G0 X239 E15 F6033.27
G0 Y12 E0.7 F1508.32
; MACHINE_START_GCODE_END
; filament start gcode

M142 P1 R35 S40
;VT0 H-1
G90
G21
M83 ; use relative distances for extrusion
M981 S1 P20000 ;open spaghetti detector
; CHANGE_LAYER
; Z_HEIGHT: 0.2
; LAYER_HEIGHT: 0.2
G1 E-.8 F1800
; layer num/total_layer_count: 1/100
; update layer progress
M73 L1
M991 S0 P0 ;notify layer change
M106 S0
M106 P2 S0
; OBJECT_ID: 15
G1 X137.143 Y135.143 F30000
M204 S6000
G1 Z.4
G1 Z.2
G1 E.8 F1800
; FEATURE: Inner wall
; LINE_WIDTH: 0.5
G1 F3000
M204 S500
G1 X118.857 Y135.143 E.68108
G1 X118.857 Y116.857 E.68108
G1 X137.143 Y116.857 E.68108
G1 X137.143 Y135.083 E.67884
M204 S6000
G1 X137.6 Y135.6 F30000
; FEATURE: Outer wall
G1 F3000
M204 S500
G1 X118.4 Y135.6 E.71513
G1 X118.4 Y116.4 E.71513
G1 X137.6 Y116.4 E.71513
G1 X137.6 Y135.54 E.71289
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 X135.6 Y135.546 E-.76
; WIPE_END
G1 E-.04 F1800
M204 S6000
G1 X135.776 Y127.916 Z.6 F30000
G1 X136.026 Y117.04 Z.6
G1 Z.2
G1 E.8 F1800
; FEATURE: Bottom surface
; LINE_WIDTH: 0.50487
G1 F6300
M204 S500
G1 X136.754 Y117.769 E.03879
G1 X136.754 Y118.422 E.02459
G1 X135.578 Y117.246 E.06262
G1 X134.925 Y117.246 E.02459
G1 X136.754 Y119.075 E.0974
G1 X136.754 Y119.728 E.02459
G1 X134.272 Y117.246 E.13217
G1 X133.618 Y117.246 E.02459
G1 X136.754 Y120.382 E.16695
G1 X136.754 Y121.035 E.02459
G1 X132.965 Y117.246 E.20173
G1 X132.312 Y117.246 E.02459
G1 X136.754 Y121.688 E.23651
G1 X136.754 Y122.342 E.02459
G1 X131.658 Y117.246 E.27129
G1 X131.005 Y117.246 E.02459
G1 X136.754 Y122.995 E.30607
G1 X136.754 Y123.648 E.02459
M73 P42 R9
G1 X130.352 Y117.246 E.34084
G1 X129.699 Y117.246 E.02459
G1 X136.754 Y124.301 E.37562
G1 X136.754 Y124.955 E.02459
G1 X129.045 Y117.246 E.4104
G1 X128.392 Y117.246 E.02459
G1 X136.754 Y125.608 E.44518
G1 X136.754 Y126.261 E.02459
G1 X127.739 Y117.246 E.47996
G1 X127.085 Y117.246 E.02459
M73 P44 R8
G1 X136.754 Y126.915 E.51474
G1 X136.754 Y127.568 E.02459
G1 X126.432 Y117.246 E.54952
G1 X125.779 Y117.246 E.02459
G1 X136.754 Y128.221 E.58429
G1 X136.754 Y128.875 E.02459
G1 X125.125 Y117.246 E.61907
G1 X124.472 Y117.246 E.02459
G1 X136.754 Y129.528 E.65385
G1 X136.754 Y130.181 E.02459
G1 X123.819 Y117.246 E.68863
G1 X123.166 Y117.246 E.02459
G1 X136.754 Y130.834 E.72341
G1 X136.754 Y131.488 E.02459
G1 X122.512 Y117.246 E.75819
M73 P45 R8
G1 X121.859 Y117.246 E.02459
G1 X136.754 Y132.141 E.79296
G1 X136.754 Y132.794 E.02459
G1 X121.206 Y117.246 E.82774
G1 X120.552 Y117.246 E.02459
G1 X136.754 Y133.448 E.86252
G1 X136.754 Y134.101 E.02459
G1 X119.899 Y117.246 E.8973
G1 X119.246 Y117.246 E.02459
G1 X136.754 Y134.754 E.93208
G1 X136.101 Y134.754 E.02458
G1 X119.246 Y117.899 E.89732
G1 X119.246 Y118.552 E.02459
G1 X135.448 Y134.754 E.86254
G1 X134.795 Y134.754 E.02459
G1 X119.246 Y119.205 E.82777
G1 X119.246 Y119.859 E.02459
G1 X134.141 Y134.754 E.79299
G1 X133.488 Y134.754 E.02459
G1 X119.246 Y120.512 E.75821
G1 X119.246 Y121.165 E.02459
G1 X132.835 Y134.754 E.72343
G1 X132.182 Y134.754 E.02459
G1 X119.246 Y121.818 E.68865
G1 X119.246 Y122.472 E.02459
G1 X131.528 Y134.754 E.65387
G1 X130.875 Y134.754 E.02459
G1 X119.246 Y123.125 E.61909
G1 X119.246 Y123.778 E.02459
G1 X130.222 Y134.754 E.58432
G1 X129.568 Y134.754 E.02459
G1 X119.246 Y124.432 E.54954
G1 X119.246 Y125.085 E.02459
G1 X128.915 Y134.754 E.51476
G1 X128.262 Y134.754 E.02459
G1 X119.246 Y125.738 E.47998
G1 X119.246 Y126.392 E.02459
G1 X127.608 Y134.754 E.4452
G1 X126.955 Y134.754 E.02459
G1 X119.246 Y127.045 E.41042
G1 X119.246 Y127.698 E.02459
G1 X126.302 Y134.754 E.37565
G1 X125.649 Y134.754 E.02459
G1 X119.246 Y128.351 E.34087
G1 X119.246 Y129.005 E.02459
M73 P46 R8
G1 X124.995 Y134.754 E.30609
G1 X124.342 Y134.754 E.02459
G1 X119.246 Y129.658 E.27131
G1 X119.246 Y130.311 E.02459
G1 X123.689 Y134.754 E.23653
G1 X123.035 Y134.754 E.02459
G1 X119.246 Y130.965 E.20175
G1 X119.246 Y131.618 E.02459
G1 X122.382 Y134.754 E.16697
G1 X121.729 Y134.754 E.02459
G1 X119.246 Y132.271 E.1322
G1 X119.246 Y132.924 E.02459
G1 X121.076 Y134.754 E.09742
G1 X120.422 Y134.754 E.02459
G1 X119.246 Y133.578 E.06264
G1 X119.246 Y134.231 E.02459
G1 X119.975 Y134.96 E.03881
; CHANGE_LAYER
; Z_HEIGHT: 0.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F6300
G1 X119.246 Y134.231 E-.39179
G1 X119.246 Y133.578 E-.24825
G1 X119.469 Y133.801 E-.11996
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 2/100
; update layer progress
M73 L2
M991 S0 P1 ;notify layer change
M106 S255
M106 P2 S178
; open powerlost recovery
M1003 S1
M976 S1 P1 ; scan model before printing 2nd layer
M400 P100
G1 E.8
; OBJECT_ID: 15
G1 E-.8
M204 S10000
G1 X127.071 Y134.478 Z.6 F30000
G1 X137.398 Y135.398 Z.6
G1 Z.4
G1 E.8 F1800
; FEATURE: Inner wall
; LINE_WIDTH: 0.45
G1 F15476.087
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F12000
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.467 Y135.234 Z.8 F30000
G1 Z.4
G1 E.8 F1800
; FEATURE: Internal solid infill
; LINE_WIDTH: 0.42222
G1 F15000
G1 X137.065 Y134.637 E.02611
G1 X137.065 Y134.101 E.01658
G1 X136.101 Y135.065 E.04214
G1 X135.564 Y135.065 E.01658
G1 X137.065 Y133.564 E.06559
G1 X137.065 Y133.028 E.01658
G1 X135.028 Y135.065 E.08903
G1 X134.491 Y135.065 E.01658
G1 X137.065 Y132.491 E.11248
G1 X137.065 Y131.955 E.01658
G1 X133.955 Y135.065 E.13593
G1 X133.419 Y135.065 E.01658
G1 X137.065 Y131.419 E.15937
G1 X137.065 Y130.882 E.01658
G1 X132.882 Y135.065 E.18282
G1 X132.346 Y135.065 E.01658
G1 X137.065 Y130.346 E.20627
M73 P47 R8
G1 X137.065 Y129.809 E.01658
G1 X131.809 Y135.065 E.22971
G1 X131.273 Y135.065 E.01658
G1 X137.065 Y129.273 E.25316
G1 X137.065 Y128.737 E.01658
G1 X130.737 Y135.065 E.27661
G1 X130.2 Y135.065 E.01658
G1 X137.065 Y128.2 E.30005
G1 X137.065 Y127.664 E.01658
G1 X129.664 Y135.065 E.3235
G1 X129.127 Y135.065 E.01658
G1 X137.065 Y127.127 E.34695
G1 X137.065 Y126.591 E.01658
G1 X128.591 Y135.065 E.3704
G1 X128.054 Y135.065 E.01658
G1 X137.065 Y126.054 E.39384
G1 X137.065 Y125.518 E.01658
G1 X127.518 Y135.065 E.41729
G1 X126.982 Y135.065 E.01658
G1 X137.065 Y124.982 E.44074
G1 X137.065 Y124.445 E.01658
G1 X126.445 Y135.065 E.46418
G1 X125.909 Y135.065 E.01658
G1 X137.065 Y123.909 E.48763
G1 X137.065 Y123.372 E.01658
G1 X125.372 Y135.065 E.51108
G1 X124.836 Y135.065 E.01658
G1 X137.065 Y122.836 E.53452
G1 X137.065 Y122.3 E.01658
G1 X124.3 Y135.065 E.55797
G1 X123.763 Y135.065 E.01658
G1 X137.065 Y121.763 E.58142
G1 X137.065 Y121.227 E.01658
G1 X123.227 Y135.065 E.60487
G1 X122.69 Y135.065 E.01658
G1 X137.065 Y120.69 E.62831
G1 X137.065 Y120.154 E.01658
G1 X122.154 Y135.065 E.65176
G1 X121.618 Y135.065 E.01658
G1 X137.065 Y119.618 E.67521
G1 X137.065 Y119.081 E.01658
G1 X121.081 Y135.065 E.69865
G1 X120.545 Y135.065 E.01658
G1 X137.065 Y118.545 E.7221
G1 X137.065 Y118.008 E.01658
G1 X120.008 Y135.065 E.74555
G1 X119.472 Y135.065 E.01658
G1 X137.065 Y117.472 E.76899
G1 X137.065 Y116.935 E.01658
G1 X118.935 Y135.065 E.79244
G1 X118.935 Y134.528 E.01657
G1 X136.528 Y116.935 E.76901
G1 X135.992 Y116.935 E.01658
G1 X118.935 Y133.992 E.74556
G1 X118.935 Y133.456 E.01658
G1 X135.456 Y116.935 E.72211
G1 X134.919 Y116.935 E.01658
G1 X118.935 Y132.919 E.69867
G1 X118.935 Y132.383 E.01658
G1 X134.383 Y116.935 E.67522
G1 X133.846 Y116.935 E.01658
G1 X118.935 Y131.846 E.65177
G1 X118.935 Y131.31 E.01658
G1 X133.31 Y116.935 E.62833
G1 X132.774 Y116.935 E.01658
G1 X118.935 Y130.774 E.60488
G1 X118.935 Y130.237 E.01658
G1 X132.237 Y116.935 E.58143
G1 X131.701 Y116.935 E.01658
G1 X118.935 Y129.701 E.55799
G1 X118.935 Y129.164 E.01658
G1 X131.164 Y116.935 E.53454
G1 X130.628 Y116.935 E.01658
G1 X118.935 Y128.628 E.51109
G1 X118.935 Y128.092 E.01658
G1 X130.092 Y116.935 E.48765
G1 X129.555 Y116.935 E.01658
G1 X118.935 Y127.555 E.4642
G1 X118.935 Y127.019 E.01658
G1 X129.019 Y116.935 E.44075
G1 X128.482 Y116.935 E.01658
G1 X118.935 Y126.482 E.4173
G1 X118.935 Y125.946 E.01658
M73 P48 R8
G1 X127.946 Y116.935 E.39386
G1 X127.409 Y116.935 E.01658
G1 X118.935 Y125.409 E.37041
G1 X118.935 Y124.873 E.01658
G1 X126.873 Y116.935 E.34696
G1 X126.337 Y116.935 E.01658
G1 X118.935 Y124.337 E.32352
G1 X118.935 Y123.8 E.01658
G1 X125.8 Y116.935 E.30007
G1 X125.264 Y116.935 E.01658
G1 X118.935 Y123.264 E.27662
G1 X118.935 Y122.727 E.01658
G1 X124.727 Y116.935 E.25318
G1 X124.191 Y116.935 E.01658
G1 X118.935 Y122.191 E.22973
G1 X118.935 Y121.655 E.01658
G1 X123.655 Y116.935 E.20628
G1 X123.118 Y116.935 E.01658
G1 X118.935 Y121.118 E.18283
G1 X118.935 Y120.582 E.01658
G1 X122.582 Y116.935 E.15939
G1 X122.045 Y116.935 E.01658
G1 X118.935 Y120.045 E.13594
G1 X118.935 Y119.509 E.01658
G1 X121.509 Y116.935 E.11249
G1 X120.973 Y116.935 E.01658
G1 X118.935 Y118.973 E.08905
G1 X118.935 Y118.436 E.01658
G1 X120.436 Y116.935 E.0656
G1 X119.9 Y116.935 E.01658
G1 X118.935 Y117.9 E.04215
G1 X118.935 Y117.363 E.01658
G1 X119.533 Y116.766 E.02612
; CHANGE_LAYER
; Z_HEIGHT: 0.6
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15000
G1 X118.935 Y117.363 E-.32117
G1 X118.935 Y117.9 E-.20384
G1 X119.373 Y117.462 E-.235
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 3/100
; update layer progress
M73 L3
M991 S0 P2 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z.8 I-.858 J.863 P1  F30000
G1 X137.398 Y135.398 Z.8
G1 Z.6
G1 E.8 F1800
; FEATURE: Inner wall
; LINE_WIDTH: 0.45
G1 F15476.087
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F12000
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.394 Y128.128 Z1 F30000
G1 X137.234 Y117.533 Z1
G1 Z.6
G1 E.8 F1800
; FEATURE: Internal solid infill
; LINE_WIDTH: 0.42222
G1 F15000
G1 X136.637 Y116.935 E.02611
G1 X136.101 Y116.935 E.01658
G1 X137.065 Y117.899 E.04214
G1 X137.065 Y118.436 E.01658
G1 X135.564 Y116.935 E.06559
G1 X135.028 Y116.935 E.01658
G1 X137.065 Y118.972 E.08903
G1 X137.065 Y119.509 E.01658
G1 X134.491 Y116.935 E.11248
G1 X133.955 Y116.935 E.01658
G1 X137.065 Y120.045 E.13593
G1 X137.065 Y120.581 E.01658
G1 X133.419 Y116.935 E.15937
G1 X132.882 Y116.935 E.01658
G1 X137.065 Y121.118 E.18282
G1 X137.065 Y121.654 E.01658
G1 X132.346 Y116.935 E.20627
G1 X131.809 Y116.935 E.01658
G1 X137.065 Y122.191 E.22971
G1 X137.065 Y122.727 E.01658
G1 X131.273 Y116.935 E.25316
G1 X130.737 Y116.935 E.01658
G1 X137.065 Y123.263 E.27661
G1 X137.065 Y123.8 E.01658
G1 X130.2 Y116.935 E.30006
G1 X129.664 Y116.935 E.01658
G1 X137.065 Y124.336 E.3235
G1 X137.065 Y124.873 E.01658
G1 X129.127 Y116.935 E.34695
G1 X128.591 Y116.935 E.01658
G1 X137.065 Y125.409 E.3704
G1 X137.065 Y125.946 E.01658
G1 X128.054 Y116.935 E.39384
G1 X127.518 Y116.935 E.01658
G1 X137.065 Y126.482 E.41729
G1 X137.065 Y127.018 E.01658
G1 X126.982 Y116.935 E.44074
G1 X126.445 Y116.935 E.01658
G1 X137.065 Y127.555 E.46418
G1 X137.065 Y128.091 E.01658
G1 X125.909 Y116.935 E.48763
G1 X125.372 Y116.935 E.01658
G1 X137.065 Y128.628 E.51108
G1 X137.065 Y129.164 E.01658
G1 X124.836 Y116.935 E.53452
G1 X124.3 Y116.935 E.01658
G1 X137.065 Y129.7 E.55797
G1 X137.065 Y130.237 E.01658
G1 X123.763 Y116.935 E.58142
G1 X123.227 Y116.935 E.01658
G1 X137.065 Y130.773 E.60486
G1 X137.065 Y131.31 E.01658
G1 X122.69 Y116.935 E.62831
G1 X122.154 Y116.935 E.01658
G1 X137.065 Y131.846 E.65176
G1 X137.065 Y132.382 E.01658
G1 X121.618 Y116.935 E.67521
G1 X121.081 Y116.935 E.01658
G1 X137.065 Y132.919 E.69865
G1 X137.065 Y133.455 E.01658
G1 X120.545 Y116.935 E.7221
G1 X120.008 Y116.935 E.01658
G1 X137.065 Y133.992 E.74555
G1 X137.065 Y134.528 E.01658
G1 X119.472 Y116.935 E.76899
G1 X118.935 Y116.935 E.01658
G1 X137.065 Y135.065 E.79244
G1 X136.528 Y135.065 E.01657
G1 X118.935 Y117.472 E.76901
G1 X118.935 Y118.008 E.01658
G1 X135.992 Y135.065 E.74556
G1 X135.456 Y135.065 E.01658
G1 X118.935 Y118.544 E.72211
G1 X118.935 Y119.081 E.01658
G1 X134.919 Y135.065 E.69867
G1 X134.383 Y135.065 E.01658
G1 X118.935 Y119.617 E.67522
G1 X118.935 Y120.154 E.01658
G1 X133.846 Y135.065 E.65177
G1 X133.31 Y135.065 E.01658
G1 X118.935 Y120.69 E.62833
G1 X118.935 Y121.226 E.01658
G1 X132.774 Y135.065 E.60488
G1 X132.237 Y135.065 E.01658
G1 X118.935 Y121.763 E.58143
G1 X118.935 Y122.299 E.01658
G1 X131.701 Y135.065 E.55799
G1 X131.164 Y135.065 E.01658
G1 X118.935 Y122.836 E.53454
G1 X118.935 Y123.372 E.01658
G1 X130.628 Y135.065 E.51109
G1 X130.092 Y135.065 E.01658
G1 X118.935 Y123.909 E.48764
G1 X118.935 Y124.445 E.01658
G1 X129.555 Y135.065 E.4642
G1 X129.019 Y135.065 E.01658
G1 X118.935 Y124.981 E.44075
G1 X118.935 Y125.518 E.01658
G1 X128.482 Y135.065 E.4173
G1 X127.946 Y135.065 E.01658
G1 X118.935 Y126.054 E.39386
G1 X118.935 Y126.591 E.01658
G1 X127.409 Y135.065 E.37041
G1 X126.873 Y135.065 E.01658
G1 X118.935 Y127.127 E.34696
G1 X118.935 Y127.663 E.01658
G1 X126.337 Y135.065 E.32352
G1 X125.8 Y135.065 E.01658
G1 X118.935 Y128.2 E.30007
G1 X118.935 Y128.736 E.01658
G1 X125.264 Y135.065 E.27662
G1 X124.727 Y135.065 E.01658
G1 X118.935 Y129.273 E.25318
G1 X118.935 Y129.809 E.01658
G1 X124.191 Y135.065 E.22973
G1 X123.655 Y135.065 E.01658
G1 X118.935 Y130.345 E.20628
G1 X118.935 Y130.882 E.01658
G1 X123.118 Y135.065 E.18284
G1 X122.582 Y135.065 E.01658
G1 X118.935 Y131.418 E.15939
G1 X118.935 Y131.955 E.01658
G1 X122.045 Y135.065 E.13594
G1 X121.509 Y135.065 E.01658
G1 X118.935 Y132.491 E.11249
G1 X118.935 Y133.027 E.01658
G1 X120.973 Y135.065 E.08905
G1 X120.436 Y135.065 E.01658
G1 X118.935 Y133.564 E.0656
G1 X118.935 Y134.1 E.01658
G1 X119.9 Y135.065 E.04215
G1 X119.363 Y135.065 E.01658
G1 X118.766 Y134.467 E.02612
; CHANGE_LAYER
; Z_HEIGHT: 0.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15000
G1 X119.363 Y135.065 E-.32117
G1 X119.9 Y135.065 E-.20384
G1 X119.462 Y134.627 E-.23499
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 4/100
; update layer progress
M73 L4
M991 S0 P3 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z1 I-.052 J1.216 P1  F30000
G1 X137.398 Y135.398 Z1
G1 Z.8
G1 E.8 F1800
; FEATURE: Inner wall
; LINE_WIDTH: 0.45
G1 F4895
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4895
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
M73 P49 R8
G1 X136.455 Y128.133 Z1.2 F30000
G1 X137.05 Y121.326 Z1.2
G1 Z.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4895
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 1
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 5/100
; update layer progress
M73 L5
M991 S0 P4 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z1.2 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z1.2
G1 Z1
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z1.4 F30000
G1 X118.95 Y121.326 Z1.4
G1 Z1
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 1.2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 6/100
; update layer progress
M73 L6
M991 S0 P5 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z1.4 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z1.4
G1 Z1.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z1.6 F30000
G1 X137.05 Y121.326 Z1.6
G1 Z1.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
M73 P49 R7
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 1.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 7/100
; update layer progress
M73 L7
M991 S0 P6 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z1.6 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z1.6
G1 Z1.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
M73 P50 R7
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z1.8 F30000
G1 X118.95 Y121.326 Z1.8
G1 Z1.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 1.6
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 8/100
; update layer progress
M73 L8
M991 S0 P7 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z1.8 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z1.8
G1 Z1.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z2 F30000
G1 X137.05 Y121.326 Z2
G1 Z1.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 1.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 9/100
; update layer progress
M73 L9
M991 S0 P8 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z2 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z2
G1 Z1.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
M73 P51 R7
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z2.2 F30000
G1 X118.95 Y121.326 Z2.2
G1 Z1.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 10/100
; update layer progress
M73 L10
M991 S0 P9 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z2.2 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z2.2
G1 Z2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z2.4 F30000
G1 X137.05 Y121.326 Z2.4
G1 Z2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 2.2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 11/100
; update layer progress
M73 L11
M991 S0 P10 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z2.4 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z2.4
G1 Z2.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z2.6 F30000
G1 X118.95 Y121.326 Z2.6
G1 Z2.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
M73 P52 R7
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 2.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 12/100
; update layer progress
M73 L12
M991 S0 P11 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z2.6 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z2.6
G1 Z2.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z2.8 F30000
G1 X137.05 Y121.326 Z2.8
G1 Z2.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 2.6
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 13/100
; update layer progress
M73 L13
M991 S0 P12 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z2.8 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z2.8
G1 Z2.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z3 F30000
G1 X118.95 Y121.326 Z3
G1 Z2.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
M73 P53 R7
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 2.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 14/100
; update layer progress
M73 L14
M991 S0 P13 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z3 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z3
G1 Z2.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z3.2 F30000
G1 X137.05 Y121.326 Z3.2
G1 Z2.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 3
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 15/100
; update layer progress
M73 L15
M991 S0 P14 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z3.2 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z3.2
G1 Z3
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z3.4 F30000
G1 X118.95 Y121.326 Z3.4
G1 Z3
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
M73 P54 R7
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 3.2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 16/100
; update layer progress
M73 L16
M991 S0 P15 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z3.4 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z3.4
G1 Z3.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z3.6 F30000
G1 X137.05 Y121.326 Z3.6
G1 Z3.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 3.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 17/100
; update layer progress
M73 L17
M991 S0 P16 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z3.6 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z3.6
G1 Z3.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z3.8 F30000
G1 X118.95 Y121.326 Z3.8
G1 Z3.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
M73 P55 R7
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 3.6
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 18/100
; update layer progress
M73 L18
M991 S0 P17 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z3.8 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z3.8
G1 Z3.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z4 F30000
G1 X137.05 Y121.326 Z4
G1 Z3.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 3.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 19/100
; update layer progress
M73 L19
M991 S0 P18 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z4 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z4
G1 Z3.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z4.2 F30000
G1 X118.95 Y121.326 Z4.2
G1 Z3.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
M73 P55 R6
G1 E-.04 F1800
; layer num/total_layer_count: 20/100
; update layer progress
M73 L20
M991 S0 P19 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z4.2 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z4.2
G1 Z4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
M73 P56 R6
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z4.4 F30000
G1 X137.05 Y121.326 Z4.4
G1 Z4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 4.2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 21/100
; update layer progress
M73 L21
M991 S0 P20 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z4.4 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z4.4
G1 Z4.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z4.6 F30000
G1 X118.95 Y121.326 Z4.6
G1 Z4.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 4.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 22/100
; update layer progress
M73 L22
M991 S0 P21 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z4.6 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z4.6
G1 Z4.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
M73 P57 R6
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z4.8 F30000
G1 X137.05 Y121.326 Z4.8
G1 Z4.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 4.6
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 23/100
; update layer progress
M73 L23
M991 S0 P22 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z4.8 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z4.8
G1 Z4.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z5 F30000
G1 X118.95 Y121.326 Z5
G1 Z4.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 4.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 24/100
; update layer progress
M73 L24
M991 S0 P23 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z5 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z5
G1 Z4.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
M73 P58 R6
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z5.2 F30000
G1 X137.05 Y121.326 Z5.2
G1 Z4.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 5
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 25/100
; update layer progress
M73 L25
M991 S0 P24 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z5.2 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z5.2
G1 Z5
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z5.4 F30000
G1 X118.95 Y121.326 Z5.4
G1 Z5
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 5.2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 26/100
; update layer progress
M73 L26
M991 S0 P25 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z5.4 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z5.4
G1 Z5.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
M73 P59 R6
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z5.6 F30000
G1 X137.05 Y121.326 Z5.6
G1 Z5.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 5.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 27/100
; update layer progress
M73 L27
M991 S0 P26 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z5.6 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z5.6
G1 Z5.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z5.8 F30000
G1 X118.95 Y121.326 Z5.8
G1 Z5.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 5.6
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 28/100
; update layer progress
M73 L28
M991 S0 P27 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z5.8 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z5.8
G1 Z5.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z6 F30000
G1 X137.05 Y121.326 Z6
G1 Z5.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
M73 P60 R6
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 5.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 29/100
; update layer progress
M73 L29
M991 S0 P28 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z6 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z6
G1 Z5.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z6.2 F30000
G1 X118.95 Y121.326 Z6.2
G1 Z5.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 6
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 30/100
; update layer progress
M73 L30
M991 S0 P29 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z6.2 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z6.2
G1 Z6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z6.4 F30000
G1 X137.05 Y121.326 Z6.4
G1 Z6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
M73 P61 R6
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 6.2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 31/100
; update layer progress
M73 L31
M991 S0 P30 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z6.4 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z6.4
G1 Z6.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z6.6 F30000
G1 X118.95 Y121.326 Z6.6
G1 Z6.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 6.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 32/100
; update layer progress
M73 L32
M991 S0 P31 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z6.6 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z6.6
G1 Z6.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z6.8 F30000
G1 X137.05 Y121.326 Z6.8
G1 Z6.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
M73 P62 R6
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 6.6
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 33/100
; update layer progress
M73 L33
M991 S0 P32 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z6.8 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z6.8
G1 Z6.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
M73 P62 R5
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z7 F30000
G1 X118.95 Y121.326 Z7
G1 Z6.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 6.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 34/100
; update layer progress
M73 L34
M991 S0 P33 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z7 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z7
G1 Z6.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z7.2 F30000
G1 X137.05 Y121.326 Z7.2
G1 Z6.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
M73 P63 R5
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 7
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 35/100
; update layer progress
M73 L35
M991 S0 P34 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z7.2 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z7.2
G1 Z7
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z7.4 F30000
G1 X118.95 Y121.326 Z7.4
G1 Z7
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 7.2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 36/100
; update layer progress
M73 L36
M991 S0 P35 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z7.4 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z7.4
G1 Z7.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z7.6 F30000
G1 X137.05 Y121.326 Z7.6
G1 Z7.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 7.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 37/100
; update layer progress
M73 L37
M991 S0 P36 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z7.6 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z7.6
G1 Z7.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
M73 P64 R5
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z7.8 F30000
G1 X118.95 Y121.326 Z7.8
G1 Z7.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 7.6
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 38/100
; update layer progress
M73 L38
M991 S0 P37 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z7.8 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z7.8
G1 Z7.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z8 F30000
G1 X137.05 Y121.326 Z8
G1 Z7.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 7.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 39/100
; update layer progress
M73 L39
M991 S0 P38 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z8 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z8
G1 Z7.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
M73 P65 R5
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z8.2 F30000
G1 X118.95 Y121.326 Z8.2
G1 Z7.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 40/100
; update layer progress
M73 L40
M991 S0 P39 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z8.2 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z8.2
G1 Z8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z8.4 F30000
G1 X137.05 Y121.326 Z8.4
G1 Z8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 8.2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 41/100
; update layer progress
M73 L41
M991 S0 P40 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z8.4 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z8.4
G1 Z8.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
M73 P66 R5
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z8.6 F30000
G1 X118.95 Y121.326 Z8.6
G1 Z8.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 8.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 42/100
; update layer progress
M73 L42
M991 S0 P41 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z8.6 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z8.6
G1 Z8.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z8.8 F30000
G1 X137.05 Y121.326 Z8.8
G1 Z8.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 8.6
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 43/100
; update layer progress
M73 L43
M991 S0 P42 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z8.8 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z8.8
G1 Z8.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
M73 P67 R5
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z9 F30000
G1 X118.95 Y121.326 Z9
G1 Z8.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 8.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 44/100
; update layer progress
M73 L44
M991 S0 P43 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z9 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z9
G1 Z8.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z9.2 F30000
G1 X137.05 Y121.326 Z9.2
G1 Z8.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 9
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 45/100
; update layer progress
M73 L45
M991 S0 P44 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z9.2 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z9.2
G1 Z9
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z9.4 F30000
G1 X118.95 Y121.326 Z9.4
G1 Z9
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
M73 P68 R5
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 9.2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 46/100
; update layer progress
M73 L46
M991 S0 P45 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z9.4 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z9.4
G1 Z9.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z9.6 F30000
G1 X137.05 Y121.326 Z9.6
G1 Z9.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
M73 P68 R4
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 9.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 47/100
; update layer progress
M73 L47
M991 S0 P46 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z9.6 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z9.6
G1 Z9.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z9.8 F30000
G1 X118.95 Y121.326 Z9.8
G1 Z9.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
M73 P69 R4
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 9.6
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 48/100
; update layer progress
M73 L48
M991 S0 P47 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z9.8 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z9.8
G1 Z9.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z10 F30000
G1 X137.05 Y121.326 Z10
G1 Z9.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 9.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 49/100
; update layer progress
M73 L49
M991 S0 P48 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z10 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z10
G1 Z9.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z10.2 F30000
G1 X118.95 Y121.326 Z10.2
G1 Z9.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
M73 P70 R4
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 10
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 50/100
; update layer progress
M73 L50
M991 S0 P49 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z10.2 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z10.2
G1 Z10
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z10.4 F30000
G1 X137.05 Y121.326 Z10.4
G1 Z10
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 10.2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 51/100
; update layer progress
M73 L51
M991 S0 P50 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z10.4 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z10.4
G1 Z10.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z10.6 F30000
G1 X118.95 Y121.326 Z10.6
G1 Z10.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
M73 P71 R4
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 10.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 52/100
; update layer progress
M73 L52
M991 S0 P51 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z10.6 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z10.6
G1 Z10.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z10.8 F30000
G1 X137.05 Y121.326 Z10.8
G1 Z10.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 10.6
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 53/100
; update layer progress
M73 L53
M991 S0 P52 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z10.8 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z10.8
G1 Z10.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z11 F30000
G1 X118.95 Y121.326 Z11
G1 Z10.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 10.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 54/100
; update layer progress
M73 L54
M991 S0 P53 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z11 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z11
G1 Z10.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
M73 P72 R4
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z11.2 F30000
G1 X137.05 Y121.326 Z11.2
G1 Z10.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 11
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 55/100
; update layer progress
M73 L55
M991 S0 P54 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z11.2 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z11.2
G1 Z11
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z11.4 F30000
G1 X118.95 Y121.326 Z11.4
G1 Z11
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 11.2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 56/100
; update layer progress
M73 L56
M991 S0 P55 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z11.4 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z11.4
G1 Z11.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
M73 P73 R4
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z11.6 F30000
G1 X137.05 Y121.326 Z11.6
G1 Z11.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 11.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 57/100
; update layer progress
M73 L57
M991 S0 P56 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z11.6 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z11.6
G1 Z11.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z11.8 F30000
G1 X118.95 Y121.326 Z11.8
G1 Z11.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 11.6
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 58/100
; update layer progress
M73 L58
M991 S0 P57 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z11.8 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z11.8
G1 Z11.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
M73 P74 R4
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z12 F30000
G1 X137.05 Y121.326 Z12
G1 Z11.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 11.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 59/100
; update layer progress
M73 L59
M991 S0 P58 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z12 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z12
G1 Z11.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z12.2 F30000
G1 X118.95 Y121.326 Z12.2
G1 Z11.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 12
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 60/100
; update layer progress
M73 L60
M991 S0 P59 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z12.2 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z12.2
G1 Z12
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
M73 P74 R3
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
M73 P75 R3
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z12.4 F30000
G1 X137.05 Y121.326 Z12.4
G1 Z12
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 12.2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 61/100
; update layer progress
M73 L61
M991 S0 P60 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z12.4 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z12.4
G1 Z12.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z12.6 F30000
G1 X118.95 Y121.326 Z12.6
G1 Z12.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 12.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 62/100
; update layer progress
M73 L62
M991 S0 P61 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z12.6 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z12.6
G1 Z12.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z12.8 F30000
G1 X137.05 Y121.326 Z12.8
G1 Z12.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
M73 P76 R3
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 12.6
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 63/100
; update layer progress
M73 L63
M991 S0 P62 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z12.8 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z12.8
G1 Z12.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z13 F30000
G1 X118.95 Y121.326 Z13
G1 Z12.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 12.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 64/100
; update layer progress
M73 L64
M991 S0 P63 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z13 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z13
G1 Z12.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z13.2 F30000
G1 X137.05 Y121.326 Z13.2
G1 Z12.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
M73 P77 R3
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 13
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 65/100
; update layer progress
M73 L65
M991 S0 P64 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z13.2 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z13.2
G1 Z13
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z13.4 F30000
G1 X118.95 Y121.326 Z13.4
G1 Z13
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 13.2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 66/100
; update layer progress
M73 L66
M991 S0 P65 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z13.4 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z13.4
G1 Z13.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z13.6 F30000
G1 X137.05 Y121.326 Z13.6
G1 Z13.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
M73 P78 R3
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 13.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 67/100
; update layer progress
M73 L67
M991 S0 P66 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z13.6 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z13.6
G1 Z13.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z13.8 F30000
G1 X118.95 Y121.326 Z13.8
G1 Z13.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 13.6
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 68/100
; update layer progress
M73 L68
M991 S0 P67 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z13.8 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z13.8
G1 Z13.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z14 F30000
G1 X137.05 Y121.326 Z14
G1 Z13.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
M73 P79 R3
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 13.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 69/100
; update layer progress
M73 L69
M991 S0 P68 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z14 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z14
G1 Z13.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z14.2 F30000
G1 X118.95 Y121.326 Z14.2
G1 Z13.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 14
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 70/100
; update layer progress
M73 L70
M991 S0 P69 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z14.2 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z14.2
G1 Z14
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z14.4 F30000
G1 X137.05 Y121.326 Z14.4
G1 Z14
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 14.2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 71/100
; update layer progress
M73 L71
M991 S0 P70 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z14.4 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z14.4
G1 Z14.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
M73 P80 R3
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z14.6 F30000
G1 X118.95 Y121.326 Z14.6
G1 Z14.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 14.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 72/100
; update layer progress
M73 L72
M991 S0 P71 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z14.6 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z14.6
G1 Z14.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z14.8 F30000
G1 X137.05 Y121.326 Z14.8
G1 Z14.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 14.6
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 73/100
; update layer progress
M73 L73
M991 S0 P72 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z14.8 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z14.8
G1 Z14.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
M73 P81 R3
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z15 F30000
G1 X118.95 Y121.326 Z15
G1 Z14.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
M73 P81 R2
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 14.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 74/100
; update layer progress
M73 L74
M991 S0 P73 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z15 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z15
G1 Z14.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z15.2 F30000
G1 X137.05 Y121.326 Z15.2
G1 Z14.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 15
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 75/100
; update layer progress
M73 L75
M991 S0 P74 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z15.2 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z15.2
G1 Z15
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
M73 P82 R2
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z15.4 F30000
G1 X118.95 Y121.326 Z15.4
G1 Z15
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 15.2
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 76/100
; update layer progress
M73 L76
M991 S0 P75 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z15.4 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z15.4
G1 Z15.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z15.6 F30000
G1 X137.05 Y121.326 Z15.6
G1 Z15.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 15.4
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 77/100
; update layer progress
M73 L77
M991 S0 P76 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z15.6 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z15.6
G1 Z15.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
M73 P83 R2
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z15.8 F30000
G1 X118.95 Y121.326 Z15.8
G1 Z15.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 15.6
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 78/100
; update layer progress
M73 L78
M991 S0 P77 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z15.8 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z15.8
G1 Z15.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z16 F30000
G1 X137.05 Y121.326 Z16
G1 Z15.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 15.8
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 79/100
; update layer progress
M73 L79
M991 S0 P78 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z16 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z16
G1 Z15.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z16.2 F30000
G1 X118.95 Y121.326 Z16.2
G1 Z15.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
M73 P84 R2
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 16
; LAYER_HEIGHT: 0.2
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 80/100
; update layer progress
M73 L80
M991 S0 P79 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z16.2 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z16.2
G1 Z16
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z16.4 F30000
G1 X137.05 Y121.326 Z16.4
G1 Z16
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 16.2
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 81/100
; update layer progress
M73 L81
M991 S0 P80 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z16.4 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z16.4
G1 Z16.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z16.6 F30000
G1 X118.95 Y121.326 Z16.6
G1 Z16.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
M73 P85 R2
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 16.4
; LAYER_HEIGHT: 0.199999
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 82/100
; update layer progress
M73 L82
M991 S0 P81 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z16.6 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z16.6
G1 Z16.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z16.8 F30000
G1 X137.05 Y121.326 Z16.8
G1 Z16.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 16.6
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 83/100
; update layer progress
M73 L83
M991 S0 P82 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z16.8 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z16.8
G1 Z16.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z17 F30000
G1 X118.95 Y121.326 Z17
G1 Z16.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
M73 P86 R2
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 16.8
; LAYER_HEIGHT: 0.199999
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 84/100
; update layer progress
M73 L84
M991 S0 P83 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z17 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z17
G1 Z16.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z17.2 F30000
G1 X137.05 Y121.326 Z17.2
G1 Z16.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 17
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 85/100
; update layer progress
M73 L85
M991 S0 P84 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z17.2 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z17.2
G1 Z17
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z17.4 F30000
G1 X118.95 Y121.326 Z17.4
G1 Z17
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
M73 P87 R2
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 17.2
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 86/100
; update layer progress
M73 L86
M991 S0 P85 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z17.4 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z17.4
G1 Z17.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z17.6 F30000
G1 X137.05 Y121.326 Z17.6
G1 Z17.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
M73 P87 R1
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 17.4
; LAYER_HEIGHT: 0.199999
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 87/100
; update layer progress
M73 L87
M991 S0 P86 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z17.6 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z17.6
G1 Z17.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z17.8 F30000
G1 X118.95 Y121.326 Z17.8
G1 Z17.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 17.6
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 88/100
; update layer progress
M73 L88
M991 S0 P87 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z17.8 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z17.8
G1 Z17.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
M73 P88 R1
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z18 F30000
G1 X137.05 Y121.326 Z18
G1 Z17.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 17.8
; LAYER_HEIGHT: 0.199999
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 89/100
; update layer progress
M73 L89
M991 S0 P88 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z18 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z18
G1 Z17.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z18.2 F30000
G1 X118.95 Y121.326 Z18.2
G1 Z17.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 18
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 90/100
; update layer progress
M73 L90
M991 S0 P89 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z18.2 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z18.2
G1 Z18
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
M73 P89 R1
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z18.4 F30000
G1 X137.05 Y121.326 Z18.4
G1 Z18
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 18.2
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 91/100
; update layer progress
M73 L91
M991 S0 P90 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z18.4 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z18.4
G1 Z18.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z18.6 F30000
G1 X118.95 Y121.326 Z18.6
G1 Z18.2
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 18.4
; LAYER_HEIGHT: 0.199999
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 92/100
; update layer progress
M73 L92
M991 S0 P91 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z18.6 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z18.6
G1 Z18.4
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
M73 P90 R1
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z18.8 F30000
G1 X137.05 Y121.326 Z18.8
G1 Z18.4
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 18.6
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 93/100
; update layer progress
M73 L93
M991 S0 P92 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z18.8 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z18.8
G1 Z18.6
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4931
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4931
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X129.991 Y130.774 Z19 F30000
G1 X118.95 Y121.326 Z19
G1 Z18.6
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4931
G1 X118.95 Y119.698 E.05401
G1 X121.698 Y116.95 E.12889
G1 X118.95 Y116.95 E.09114
G1 X137.05 Y135.05 E.84908
G1 X137.05 Y132.302 E.09114
G1 X134.302 Y135.05 E.12889
G1 X129.374 Y135.05 E.16349
G1 X118.95 Y124.626 E.48898
G1 X118.95 Y127.374 E.09114
G1 X129.374 Y116.95 E.48898
G1 X126.626 Y116.95 E.09114
G1 X137.05 Y127.374 E.48898
G1 X137.05 Y124.626 E.09114
G1 X126.626 Y135.05 E.48898
G1 X121.698 Y135.05 E.16349
G1 X118.95 Y132.302 E.12889
G1 X118.95 Y135.05 E.09114
G1 X137.05 Y116.95 E.84908
G1 X134.302 Y116.95 E.09114
G1 X137.05 Y119.698 E.12889
G1 X137.05 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 18.8
; LAYER_HEIGHT: 0.199999
; WIPE_START
G1 F15476.087
G1 X137.05 Y119.698 E-.61876
G1 X136.787 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 94/100
; update layer progress
M73 L94
M991 S0 P93 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z19 I-1.216 J.047 P1  F30000
G1 X137.398 Y135.398 Z19
G1 Z18.8
G1 E.8 F1800
; FEATURE: Inner wall
G1 F4890
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F4890
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
M73 P91 R1
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.455 Y128.133 Z19.2 F30000
G1 X137.05 Y121.326 Z19.2
G1 Z18.8
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F4890
G1 X137.05 Y119.698 E.05401
G1 X134.302 Y116.95 E.12889
G1 X137.05 Y116.95 E.09114
G1 X118.95 Y135.05 E.84908
G1 X118.95 Y132.302 E.09114
G1 X121.698 Y135.05 E.12889
G1 X126.626 Y135.05 E.16349
G1 X137.05 Y124.626 E.48898
G1 X137.05 Y127.374 E.09114
G1 X126.626 Y116.95 E.48898
G1 X129.374 Y116.95 E.09114
G1 X118.95 Y127.374 E.48898
G1 X118.95 Y124.626 E.09114
G1 X129.374 Y135.05 E.48898
G1 X134.302 Y135.05 E.16349
G1 X137.05 Y132.302 E.12889
G1 X137.05 Y135.05 E.09114
G1 X118.95 Y116.95 E.84908
G1 X121.698 Y116.95 E.09114
G1 X118.95 Y119.698 E.12889
G1 X118.95 Y121.326 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 19
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X118.95 Y119.698 E-.61876
G1 X119.213 Y119.435 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 95/100
; update layer progress
M73 L95
M991 S0 P94 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z19.2 I-.803 J.915 P1  F30000
G1 X137.398 Y135.398 Z19.2
G1 Z19
G1 E.8 F1800
; FEATURE: Inner wall
G1 F7531
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F7531
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
G1 F12000
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X130.625 Y134.673 Z19.4 F30000
G1 Z19
G1 E.8 F1800
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F7531
G1 X128.997 Y134.673 E.05401
G1 X119.327 Y125.003 E.4536
G1 X119.327 Y126.997 E.06611
G1 X128.997 Y117.328 E.45359
G1 X127.003 Y117.328 E.06611
G1 X136.673 Y126.997 E.4536
G1 X136.673 Y125.003 E.06612
G1 X127.003 Y134.673 E.4536
G1 X121.321 Y134.673 E.18851
G1 X119.327 Y132.679 E.0935
G1 X119.327 Y134.672 E.06611
G1 X136.672 Y117.328 E.81369
G1 X134.679 Y117.328 E.06611
G1 X136.673 Y119.321 E.0935
G1 X136.673 Y120.949 E.05401
G1 X137.008 Y116.992 F30000
; Slow Down Start
; FEATURE: Floating vertical shell
; LINE_WIDTH: 0.399323
G1 F3000;_EXTRUDE_SET_SPEED
G1 X137.035 Y117.124 E.00391
G1 X137.035 Y134.876 E.51556
G1 X137.008 Y135.008 E.00391
G1 X136.876 Y135.035 E.00391
G1 X119.124 Y135.035 E.51556
G1 X118.992 Y135.008 E.00391
G1 X118.965 Y134.876 E.00391
G1 X118.965 Y117.124 E.51556
G1 X118.992 Y116.992 E.00391
G1 X119.124 Y116.965 E.00391
G1 X136.876 Y116.965 E.51556
G1 X136.949 Y116.98 E.00217
; Slow Down End
G1 X119.327 Y120.949 F30000
; FEATURE: Sparse infill
; LINE_WIDTH: 0.45
G1 F7531
G1 X119.327 Y119.321 E.05401
G1 X121.321 Y117.328 E.0935
G1 X119.327 Y117.328 E.06611
G1 X136.673 Y134.673 E.81369
G1 X134.679 Y134.673 E.06612
G1 X136.673 Y132.679 E.0935
G1 X136.673 Y131.051 E.05401
; CHANGE_LAYER
; Z_HEIGHT: 19.2
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15476.087
G1 X136.673 Y132.679 E-.61876
G1 X136.41 Y132.942 E-.14124
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 96/100
; update layer progress
M73 L96
M991 S0 P95 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z19.4 I-1.129 J.454 P1  F30000
G1 X137.398 Y135.398 Z19.4
G1 Z19.2
G1 E.8 F1800
; FEATURE: Inner wall
G1 F15476.087
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F12000
M204 S5000
G1 X118.21 Y135.79 E.60164
M73 P92 R1
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.318 Y135.231 Z19.6 F30000
G1 Z19.2
G1 E.8 F1800
; FEATURE: Bridge
; LINE_WIDTH: 0.40124
; LAYER_HEIGHT: 0.4
G1 F3000
G1 X137.028 Y134.521 E.05171
G1 X137.028 Y133.883 E.03288
G1 X135.883 Y135.028 E.08345
G1 X135.245 Y135.028 E.03288
G1 X137.028 Y133.245 E.12995
G1 X137.028 Y132.606 E.03288
G1 X134.606 Y135.028 E.17644
G1 X133.968 Y135.028 E.03288
G1 X137.028 Y131.968 E.22294
G1 X137.028 Y131.33 E.03288
G1 X133.33 Y135.028 E.26943
G1 X132.692 Y135.028 E.03288
G1 X137.028 Y130.692 E.31592
G1 X137.028 Y130.054 E.03288
G1 X132.054 Y135.028 E.36242
G1 X131.416 Y135.028 E.03288
G1 X137.028 Y129.416 E.40891
G1 X137.028 Y128.778 E.03288
G1 X130.778 Y135.028 E.45541
G1 X130.139 Y135.028 E.03288
G1 X137.028 Y128.139 E.5019
G1 X137.028 Y127.501 E.03288
G1 X129.501 Y135.028 E.54839
G1 X128.863 Y135.028 E.03288
G1 X137.028 Y126.863 E.59489
G1 X137.028 Y126.225 E.03288
G1 X128.225 Y135.028 E.64138
G1 X127.587 Y135.028 E.03288
G1 X137.028 Y125.587 E.68788
G1 X137.028 Y124.949 E.03288
G1 X126.949 Y135.028 E.73437
G1 X126.311 Y135.028 E.03288
G1 X137.028 Y124.311 E.78086
G1 X137.028 Y123.672 E.03288
G1 X125.672 Y135.028 E.82736
G1 X125.034 Y135.028 E.03288
G1 X137.028 Y123.034 E.87385
G1 X137.028 Y122.396 E.03288
G1 X124.396 Y135.028 E.92035
G1 X123.758 Y135.028 E.03288
G1 X137.028 Y121.758 E.96684
G1 X137.028 Y121.12 E.03288
G1 X123.12 Y135.028 E1.01333
G1 X122.482 Y135.028 E.03288
G1 X137.028 Y120.482 E1.05983
G1 X137.028 Y119.843 E.03288
G1 X121.843 Y135.028 E1.10632
G1 X121.205 Y135.028 E.03288
G1 X137.028 Y119.205 E1.15281
G1 X137.028 Y118.567 E.03288
G1 X120.567 Y135.028 E1.19931
G1 X119.929 Y135.028 E.03288
G1 X137.028 Y117.929 E1.2458
G1 X137.028 Y117.291 E.03288
G1 X119.291 Y135.028 E1.2923
G1 X118.972 Y135.028 E.01644
G1 X118.972 Y134.709 E.01644
G1 X136.709 Y116.972 E1.2923
G1 X136.071 Y116.972 E.03288
G1 X118.972 Y134.071 E1.2458
G1 X118.972 Y133.433 E.03288
G1 X135.433 Y116.972 E1.19931
G1 X134.795 Y116.972 E.03288
G1 X118.972 Y132.795 E1.15282
G1 X118.972 Y132.157 E.03288
G1 X134.157 Y116.972 E1.10632
G1 X133.518 Y116.972 E.03288
G1 X118.972 Y131.518 E1.05983
G1 X118.972 Y130.88 E.03288
G1 X132.88 Y116.972 E1.01333
G1 X132.242 Y116.972 E.03288
G1 X118.972 Y130.242 E.96684
G1 X118.972 Y129.604 E.03288
G1 X131.604 Y116.972 E.92035
G1 X130.966 Y116.972 E.03288
G1 X118.972 Y128.966 E.87385
G1 X118.972 Y128.328 E.03288
G1 X130.328 Y116.972 E.82736
G1 X129.69 Y116.972 E.03288
G1 X118.972 Y127.69 E.78086
G1 X118.972 Y127.051 E.03288
G1 X129.051 Y116.972 E.73437
G1 X128.413 Y116.972 E.03288
G1 X118.972 Y126.413 E.68788
G1 X118.972 Y125.775 E.03288
G1 X127.775 Y116.972 E.64138
G1 X127.137 Y116.972 E.03288
G1 X118.972 Y125.137 E.59489
G1 X118.972 Y124.499 E.03288
G1 X126.499 Y116.972 E.54839
G1 X125.861 Y116.972 E.03288
G1 X118.972 Y123.861 E.5019
G1 X118.972 Y123.222 E.03288
G1 X125.222 Y116.972 E.45541
G1 X124.584 Y116.972 E.03288
G1 X118.972 Y122.584 E.40891
G1 X118.972 Y121.946 E.03288
G1 X123.946 Y116.972 E.36242
G1 X123.308 Y116.972 E.03288
G1 X118.972 Y121.308 E.31592
G1 X118.972 Y120.67 E.03288
G1 X122.67 Y116.972 E.26943
G1 X122.032 Y116.972 E.03288
G1 X118.972 Y120.032 E.22294
G1 X118.972 Y119.394 E.03288
G1 X121.394 Y116.972 E.17644
G1 X120.755 Y116.972 E.03288
G1 X118.972 Y118.755 E.12995
G1 X118.972 Y118.117 E.03288
G1 X120.117 Y116.972 E.08346
G1 X119.479 Y116.972 E.03288
G1 X118.769 Y117.682 E.05171
; CHANGE_LAYER
; Z_HEIGHT: 19.4
; LAYER_HEIGHT: 0.199999
; WIPE_START
G1 F3000
G1 X119.479 Y116.972 E-.38145
G1 X120.117 Y116.972 E-.2425
G1 X119.864 Y117.225 E-.13605
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 97/100
; update layer progress
M73 L97
M991 S0 P96 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z19.6 I-.876 J.845 P1  F30000
G1 X137.398 Y135.398 Z19.6
G1 Z19.4
G1 E.8 F1800
; FEATURE: Inner wall
; LINE_WIDTH: 0.45
G1 F15476.087
M73 P93 R1
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F12000
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.394 Y128.128 Z19.8 F30000
G1 X137.234 Y117.533 Z19.8
G1 Z19.4
G1 E.8 F1800
; FEATURE: Internal solid infill
; LINE_WIDTH: 0.42222
G1 F15000
G1 X136.637 Y116.935 E.02611
G1 X136.101 Y116.935 E.01658
G1 X137.065 Y117.899 E.04214
G1 X137.065 Y118.436 E.01658
G1 X135.564 Y116.935 E.06559
G1 X135.028 Y116.935 E.01658
G1 X137.065 Y118.972 E.08903
G1 X137.065 Y119.509 E.01658
G1 X134.491 Y116.935 E.11248
G1 X133.955 Y116.935 E.01658
G1 X137.065 Y120.045 E.13593
G1 X137.065 Y120.581 E.01658
G1 X133.419 Y116.935 E.15937
G1 X132.882 Y116.935 E.01658
G1 X137.065 Y121.118 E.18282
G1 X137.065 Y121.654 E.01658
G1 X132.346 Y116.935 E.20627
G1 X131.809 Y116.935 E.01658
G1 X137.065 Y122.191 E.22971
G1 X137.065 Y122.727 E.01658
M73 P93 R0
G1 X131.273 Y116.935 E.25316
G1 X130.737 Y116.935 E.01658
G1 X137.065 Y123.263 E.27661
G1 X137.065 Y123.8 E.01658
G1 X130.2 Y116.935 E.30006
G1 X129.664 Y116.935 E.01658
G1 X137.065 Y124.336 E.3235
G1 X137.065 Y124.873 E.01658
G1 X129.127 Y116.935 E.34695
G1 X128.591 Y116.935 E.01658
M73 P94 R0
G1 X137.065 Y125.409 E.3704
G1 X137.065 Y125.946 E.01658
G1 X128.054 Y116.935 E.39384
G1 X127.518 Y116.935 E.01658
G1 X137.065 Y126.482 E.41729
G1 X137.065 Y127.018 E.01658
G1 X126.982 Y116.935 E.44074
G1 X126.445 Y116.935 E.01658
G1 X137.065 Y127.555 E.46418
G1 X137.065 Y128.091 E.01658
G1 X125.909 Y116.935 E.48763
G1 X125.372 Y116.935 E.01658
G1 X137.065 Y128.628 E.51108
G1 X137.065 Y129.164 E.01658
G1 X124.836 Y116.935 E.53452
G1 X124.3 Y116.935 E.01658
G1 X137.065 Y129.7 E.55797
G1 X137.065 Y130.237 E.01658
G1 X123.763 Y116.935 E.58142
G1 X123.227 Y116.935 E.01658
G1 X137.065 Y130.773 E.60486
G1 X137.065 Y131.31 E.01658
G1 X122.69 Y116.935 E.62831
G1 X122.154 Y116.935 E.01658
G1 X137.065 Y131.846 E.65176
G1 X137.065 Y132.382 E.01658
G1 X121.618 Y116.935 E.67521
G1 X121.081 Y116.935 E.01658
G1 X137.065 Y132.919 E.69865
G1 X137.065 Y133.455 E.01658
G1 X120.545 Y116.935 E.7221
G1 X120.008 Y116.935 E.01658
G1 X137.065 Y133.992 E.74555
G1 X137.065 Y134.528 E.01658
G1 X119.472 Y116.935 E.76899
G1 X118.935 Y116.935 E.01658
G1 X137.065 Y135.065 E.79244
G1 X136.528 Y135.065 E.01657
G1 X118.935 Y117.472 E.76901
G1 X118.935 Y118.008 E.01658
G1 X135.992 Y135.065 E.74556
G1 X135.456 Y135.065 E.01658
G1 X118.935 Y118.544 E.72211
G1 X118.935 Y119.081 E.01658
G1 X134.919 Y135.065 E.69867
G1 X134.383 Y135.065 E.01658
G1 X118.935 Y119.617 E.67522
G1 X118.935 Y120.154 E.01658
G1 X133.846 Y135.065 E.65177
G1 X133.31 Y135.065 E.01658
G1 X118.935 Y120.69 E.62833
G1 X118.935 Y121.226 E.01658
G1 X132.774 Y135.065 E.60488
G1 X132.237 Y135.065 E.01658
G1 X118.935 Y121.763 E.58143
G1 X118.935 Y122.299 E.01658
G1 X131.701 Y135.065 E.55799
G1 X131.164 Y135.065 E.01658
G1 X118.935 Y122.836 E.53454
G1 X118.935 Y123.372 E.01658
G1 X130.628 Y135.065 E.51109
G1 X130.092 Y135.065 E.01658
G1 X118.935 Y123.909 E.48764
G1 X118.935 Y124.445 E.01658
G1 X129.555 Y135.065 E.4642
G1 X129.019 Y135.065 E.01658
G1 X118.935 Y124.981 E.44075
G1 X118.935 Y125.518 E.01658
G1 X128.482 Y135.065 E.4173
G1 X127.946 Y135.065 E.01658
G1 X118.935 Y126.054 E.39386
G1 X118.935 Y126.591 E.01658
G1 X127.409 Y135.065 E.37041
G1 X126.873 Y135.065 E.01658
G1 X118.935 Y127.127 E.34696
G1 X118.935 Y127.663 E.01658
G1 X126.337 Y135.065 E.32352
G1 X125.8 Y135.065 E.01658
G1 X118.935 Y128.2 E.30007
G1 X118.935 Y128.736 E.01658
G1 X125.264 Y135.065 E.27662
G1 X124.727 Y135.065 E.01658
G1 X118.935 Y129.273 E.25318
G1 X118.935 Y129.809 E.01658
G1 X124.191 Y135.065 E.22973
G1 X123.655 Y135.065 E.01658
G1 X118.935 Y130.345 E.20628
G1 X118.935 Y130.882 E.01658
G1 X123.118 Y135.065 E.18284
G1 X122.582 Y135.065 E.01658
G1 X118.935 Y131.418 E.15939
G1 X118.935 Y131.955 E.01658
G1 X122.045 Y135.065 E.13594
G1 X121.509 Y135.065 E.01658
G1 X118.935 Y132.491 E.11249
G1 X118.935 Y133.027 E.01658
G1 X120.973 Y135.065 E.08905
G1 X120.436 Y135.065 E.01658
G1 X118.935 Y133.564 E.0656
G1 X118.935 Y134.1 E.01658
G1 X119.9 Y135.065 E.04215
G1 X119.363 Y135.065 E.01658
G1 X118.766 Y134.467 E.02612
; CHANGE_LAYER
; Z_HEIGHT: 19.6
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15000
G1 X119.363 Y135.065 E-.32117
G1 X119.9 Y135.065 E-.20384
G1 X119.462 Y134.627 E-.23499
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 98/100
; update layer progress
M73 L98
M991 S0 P97 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z19.8 I-.052 J1.216 P1  F30000
G1 X137.398 Y135.398 Z19.8
G1 Z19.6
G1 E.8 F1800
; FEATURE: Inner wall
; LINE_WIDTH: 0.45
G1 F15476.087
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F12000
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.467 Y135.234 Z20 F30000
G1 Z19.6
G1 E.8 F1800
; FEATURE: Internal solid infill
; LINE_WIDTH: 0.42222
G1 F15000
G1 X137.065 Y134.637 E.02611
G1 X137.065 Y134.101 E.01658
G1 X136.101 Y135.065 E.04214
G1 X135.564 Y135.065 E.01658
G1 X137.065 Y133.564 E.06559
G1 X137.065 Y133.028 E.01658
G1 X135.028 Y135.065 E.08903
G1 X134.491 Y135.065 E.01658
G1 X137.065 Y132.491 E.11248
G1 X137.065 Y131.955 E.01658
G1 X133.955 Y135.065 E.13593
G1 X133.419 Y135.065 E.01658
G1 X137.065 Y131.419 E.15937
G1 X137.065 Y130.882 E.01658
G1 X132.882 Y135.065 E.18282
G1 X132.346 Y135.065 E.01658
G1 X137.065 Y130.346 E.20627
G1 X137.065 Y129.809 E.01658
G1 X131.809 Y135.065 E.22971
M73 P95 R0
G1 X131.273 Y135.065 E.01658
G1 X137.065 Y129.273 E.25316
G1 X137.065 Y128.737 E.01658
G1 X130.737 Y135.065 E.27661
G1 X130.2 Y135.065 E.01658
G1 X137.065 Y128.2 E.30005
G1 X137.065 Y127.664 E.01658
G1 X129.664 Y135.065 E.3235
G1 X129.127 Y135.065 E.01658
G1 X137.065 Y127.127 E.34695
G1 X137.065 Y126.591 E.01658
G1 X128.591 Y135.065 E.3704
G1 X128.054 Y135.065 E.01658
G1 X137.065 Y126.054 E.39384
G1 X137.065 Y125.518 E.01658
G1 X127.518 Y135.065 E.41729
G1 X126.982 Y135.065 E.01658
G1 X137.065 Y124.982 E.44074
G1 X137.065 Y124.445 E.01658
G1 X126.445 Y135.065 E.46418
G1 X125.909 Y135.065 E.01658
G1 X137.065 Y123.909 E.48763
G1 X137.065 Y123.372 E.01658
G1 X125.372 Y135.065 E.51108
G1 X124.836 Y135.065 E.01658
G1 X137.065 Y122.836 E.53452
G1 X137.065 Y122.3 E.01658
G1 X124.3 Y135.065 E.55797
G1 X123.763 Y135.065 E.01658
G1 X137.065 Y121.763 E.58142
G1 X137.065 Y121.227 E.01658
G1 X123.227 Y135.065 E.60487
G1 X122.69 Y135.065 E.01658
G1 X137.065 Y120.69 E.62831
G1 X137.065 Y120.154 E.01658
G1 X122.154 Y135.065 E.65176
G1 X121.618 Y135.065 E.01658
G1 X137.065 Y119.618 E.67521
G1 X137.065 Y119.081 E.01658
G1 X121.081 Y135.065 E.69865
G1 X120.545 Y135.065 E.01658
G1 X137.065 Y118.545 E.7221
G1 X137.065 Y118.008 E.01658
G1 X120.008 Y135.065 E.74555
G1 X119.472 Y135.065 E.01658
G1 X137.065 Y117.472 E.76899
G1 X137.065 Y116.935 E.01658
G1 X118.935 Y135.065 E.79244
G1 X118.935 Y134.528 E.01657
G1 X136.528 Y116.935 E.76901
G1 X135.992 Y116.935 E.01658
G1 X118.935 Y133.992 E.74556
G1 X118.935 Y133.456 E.01658
G1 X135.456 Y116.935 E.72211
G1 X134.919 Y116.935 E.01658
G1 X118.935 Y132.919 E.69867
G1 X118.935 Y132.383 E.01658
G1 X134.383 Y116.935 E.67522
G1 X133.846 Y116.935 E.01658
G1 X118.935 Y131.846 E.65177
G1 X118.935 Y131.31 E.01658
G1 X133.31 Y116.935 E.62833
G1 X132.774 Y116.935 E.01658
G1 X118.935 Y130.774 E.60488
G1 X118.935 Y130.237 E.01658
G1 X132.237 Y116.935 E.58143
G1 X131.701 Y116.935 E.01658
G1 X118.935 Y129.701 E.55799
G1 X118.935 Y129.164 E.01658
G1 X131.164 Y116.935 E.53454
G1 X130.628 Y116.935 E.01658
G1 X118.935 Y128.628 E.51109
G1 X118.935 Y128.092 E.01658
G1 X130.092 Y116.935 E.48765
G1 X129.555 Y116.935 E.01658
G1 X118.935 Y127.555 E.4642
G1 X118.935 Y127.019 E.01658
G1 X129.019 Y116.935 E.44075
G1 X128.482 Y116.935 E.01658
G1 X118.935 Y126.482 E.4173
G1 X118.935 Y125.946 E.01658
G1 X127.946 Y116.935 E.39386
G1 X127.409 Y116.935 E.01658
G1 X118.935 Y125.409 E.37041
G1 X118.935 Y124.873 E.01658
G1 X126.873 Y116.935 E.34696
G1 X126.337 Y116.935 E.01658
G1 X118.935 Y124.337 E.32352
G1 X118.935 Y123.8 E.01658
G1 X125.8 Y116.935 E.30007
G1 X125.264 Y116.935 E.01658
G1 X118.935 Y123.264 E.27662
G1 X118.935 Y122.727 E.01658
G1 X124.727 Y116.935 E.25318
G1 X124.191 Y116.935 E.01658
G1 X118.935 Y122.191 E.22973
G1 X118.935 Y121.655 E.01658
G1 X123.655 Y116.935 E.20628
G1 X123.118 Y116.935 E.01658
G1 X118.935 Y121.118 E.18283
G1 X118.935 Y120.582 E.01658
G1 X122.582 Y116.935 E.15939
G1 X122.045 Y116.935 E.01658
G1 X118.935 Y120.045 E.13594
G1 X118.935 Y119.509 E.01658
G1 X121.509 Y116.935 E.11249
G1 X120.973 Y116.935 E.01658
G1 X118.935 Y118.973 E.08905
G1 X118.935 Y118.436 E.01658
G1 X120.436 Y116.935 E.0656
G1 X119.9 Y116.935 E.01658
G1 X118.935 Y117.9 E.04215
G1 X118.935 Y117.363 E.01658
G1 X119.533 Y116.766 E.02612
; CHANGE_LAYER
; Z_HEIGHT: 19.8
; LAYER_HEIGHT: 0.199999
; WIPE_START
G1 F15000
G1 X118.935 Y117.363 E-.32117
G1 X118.935 Y117.9 E-.20384
G1 X119.373 Y117.462 E-.235
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 99/100
; update layer progress
M73 L99
M991 S0 P98 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z20 I-.858 J.863 P1  F30000
G1 X137.398 Y135.398 Z20
G1 Z19.8
G1 E.8 F1800
; FEATURE: Inner wall
; LINE_WIDTH: 0.45
G1 F15476.087
G1 X118.602 Y135.398 E.62349
G1 X118.602 Y116.602 E.62349
G1 X137.398 Y116.602 E.62349
G1 X137.398 Y135.338 E.6215
M204 S250
G1 X137.79 Y135.79 F30000
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F12000
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

; WIPE_START
M204 S10000
G1 X135.79 Y135.736 E-.76
; WIPE_END
G1 E-.04 F1800
G1 X136.394 Y128.128 Z20.2 F30000
G1 X137.234 Y117.533 Z20.2
G1 Z19.8
G1 E.8 F1800
; FEATURE: Internal solid infill
; LINE_WIDTH: 0.42222
G1 F15000
G1 X136.637 Y116.935 E.02611
G1 X136.101 Y116.935 E.01658
G1 X137.065 Y117.899 E.04214
G1 X137.065 Y118.436 E.01658
G1 X135.564 Y116.935 E.06559
G1 X135.028 Y116.935 E.01658
G1 X137.065 Y118.972 E.08903
G1 X137.065 Y119.509 E.01658
G1 X134.491 Y116.935 E.11248
G1 X133.955 Y116.935 E.01658
G1 X137.065 Y120.045 E.13593
G1 X137.065 Y120.581 E.01658
G1 X133.419 Y116.935 E.15937
G1 X132.882 Y116.935 E.01658
G1 X137.065 Y121.118 E.18282
G1 X137.065 Y121.654 E.01658
G1 X132.346 Y116.935 E.20627
G1 X131.809 Y116.935 E.01658
G1 X137.065 Y122.191 E.22971
G1 X137.065 Y122.727 E.01658
G1 X131.273 Y116.935 E.25316
G1 X130.737 Y116.935 E.01658
G1 X137.065 Y123.263 E.27661
G1 X137.065 Y123.8 E.01658
G1 X130.2 Y116.935 E.30006
G1 X129.664 Y116.935 E.01658
G1 X137.065 Y124.336 E.3235
G1 X137.065 Y124.873 E.01658
G1 X129.127 Y116.935 E.34695
G1 X128.591 Y116.935 E.01658
G1 X137.065 Y125.409 E.3704
G1 X137.065 Y125.946 E.01658
G1 X128.054 Y116.935 E.39384
G1 X127.518 Y116.935 E.01658
G1 X137.065 Y126.482 E.41729
G1 X137.065 Y127.018 E.01658
G1 X126.982 Y116.935 E.44074
G1 X126.445 Y116.935 E.01658
G1 X137.065 Y127.555 E.46418
G1 X137.065 Y128.091 E.01658
G1 X125.909 Y116.935 E.48763
G1 X125.372 Y116.935 E.01658
G1 X137.065 Y128.628 E.51108
G1 X137.065 Y129.164 E.01658
G1 X124.836 Y116.935 E.53452
G1 X124.3 Y116.935 E.01658
G1 X137.065 Y129.7 E.55797
G1 X137.065 Y130.237 E.01658
G1 X123.763 Y116.935 E.58142
G1 X123.227 Y116.935 E.01658
G1 X137.065 Y130.773 E.60486
G1 X137.065 Y131.31 E.01658
G1 X122.69 Y116.935 E.62831
G1 X122.154 Y116.935 E.01658
G1 X137.065 Y131.846 E.65176
G1 X137.065 Y132.382 E.01658
G1 X121.618 Y116.935 E.67521
G1 X121.081 Y116.935 E.01658
G1 X137.065 Y132.919 E.69865
G1 X137.065 Y133.455 E.01658
G1 X120.545 Y116.935 E.7221
G1 X120.008 Y116.935 E.01658
G1 X137.065 Y133.992 E.74555
G1 X137.065 Y134.528 E.01658
G1 X119.472 Y116.935 E.76899
G1 X118.935 Y116.935 E.01658
G1 X137.065 Y135.065 E.79244
G1 X136.528 Y135.065 E.01657
G1 X118.935 Y117.472 E.76901
G1 X118.935 Y118.008 E.01658
G1 X135.992 Y135.065 E.74556
G1 X135.456 Y135.065 E.01658
G1 X118.935 Y118.544 E.72211
G1 X118.935 Y119.081 E.01658
G1 X134.919 Y135.065 E.69867
G1 X134.383 Y135.065 E.01658
G1 X118.935 Y119.617 E.67522
G1 X118.935 Y120.154 E.01658
G1 X133.846 Y135.065 E.65177
G1 X133.31 Y135.065 E.01658
G1 X118.935 Y120.69 E.62833
G1 X118.935 Y121.226 E.01658
M73 P96 R0
G1 X132.774 Y135.065 E.60488
G1 X132.237 Y135.065 E.01658
G1 X118.935 Y121.763 E.58143
G1 X118.935 Y122.299 E.01658
G1 X131.701 Y135.065 E.55799
G1 X131.164 Y135.065 E.01658
G1 X118.935 Y122.836 E.53454
G1 X118.935 Y123.372 E.01658
G1 X130.628 Y135.065 E.51109
G1 X130.092 Y135.065 E.01658
G1 X118.935 Y123.909 E.48764
G1 X118.935 Y124.445 E.01658
G1 X129.555 Y135.065 E.4642
G1 X129.019 Y135.065 E.01658
G1 X118.935 Y124.981 E.44075
G1 X118.935 Y125.518 E.01658
G1 X128.482 Y135.065 E.4173
G1 X127.946 Y135.065 E.01658
G1 X118.935 Y126.054 E.39386
G1 X118.935 Y126.591 E.01658
G1 X127.409 Y135.065 E.37041
G1 X126.873 Y135.065 E.01658
G1 X118.935 Y127.127 E.34696
G1 X118.935 Y127.663 E.01658
G1 X126.337 Y135.065 E.32352
G1 X125.8 Y135.065 E.01658
G1 X118.935 Y128.2 E.30007
G1 X118.935 Y128.736 E.01658
G1 X125.264 Y135.065 E.27662
G1 X124.727 Y135.065 E.01658
G1 X118.935 Y129.273 E.25318
G1 X118.935 Y129.809 E.01658
G1 X124.191 Y135.065 E.22973
G1 X123.655 Y135.065 E.01658
G1 X118.935 Y130.345 E.20628
G1 X118.935 Y130.882 E.01658
G1 X123.118 Y135.065 E.18284
G1 X122.582 Y135.065 E.01658
G1 X118.935 Y131.418 E.15939
G1 X118.935 Y131.955 E.01658
G1 X122.045 Y135.065 E.13594
G1 X121.509 Y135.065 E.01658
G1 X118.935 Y132.491 E.11249
G1 X118.935 Y133.027 E.01658
G1 X120.973 Y135.065 E.08905
G1 X120.436 Y135.065 E.01658
G1 X118.935 Y133.564 E.0656
G1 X118.935 Y134.1 E.01658
G1 X119.9 Y135.065 E.04215
G1 X119.363 Y135.065 E.01658
G1 X118.766 Y134.467 E.02612
; CHANGE_LAYER
; Z_HEIGHT: 20
; LAYER_HEIGHT: 0.200001
; WIPE_START
G1 F15000
G1 X119.363 Y135.065 E-.32117
G1 X119.9 Y135.065 E-.20384
G1 X119.462 Y134.627 E-.23499
; WIPE_END
G1 E-.04 F1800
; layer num/total_layer_count: 100/100
; update layer progress
M73 L100
M991 S0 P99 ;notify layer change
; OBJECT_ID: 15
G17
G3 Z20.2 I-.077 J1.215 P1  F30000
G1 X137.79 Y135.79 Z20.2
G1 Z20
G1 E.8 F1800
; FEATURE: Outer wall
; LINE_WIDTH: 0.42
G1 F12000
M204 S5000
G1 X118.21 Y135.79 E.60164
G1 X118.21 Y116.21 E.60164
G1 X137.79 Y116.21 E.60164
G1 X137.79 Y135.73 E.5998
;========Date 20250206========
; SKIPPABLE_START
; SKIPTYPE: timelapse
M622.1 S1 ; for prev firmware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
 ; timelapse without wipe tower
M971 S11 C10 O0
M1004 S5 P1  ; external shutter

M623
; SKIPPABLE_END

M204 S10000
G1 X137.583 Y134.815 F30000
; FEATURE: Top surface
G1 F12000
M204 S2000
G1 X136.815 Y135.583 E.03337
G1 X136.281 Y135.583
G1 X137.583 Y134.281 E.05654
G1 X137.583 Y133.748
G1 X135.748 Y135.583 E.07972
G1 X135.215 Y135.583
G1 X137.583 Y133.215 E.10289
G1 X137.583 Y132.682
G1 X134.682 Y135.583 E.12606
G1 X134.148 Y135.583
G1 X137.583 Y132.148 E.14923
G1 X137.583 Y131.615
G1 X133.615 Y135.583 E.17241
G1 X133.082 Y135.583
G1 X137.583 Y131.082 E.19558
G1 X137.583 Y130.549
G1 X132.549 Y135.583 E.21875
G1 X132.015 Y135.583
G1 X137.583 Y130.015 E.24193
G1 X137.583 Y129.482
G1 X131.482 Y135.583 E.2651
G1 X130.949 Y135.583
G1 X137.583 Y128.949 E.28827
G1 X137.583 Y128.416
G1 X130.416 Y135.583 E.31144
G1 X129.882 Y135.583
G1 X137.583 Y127.882 E.33462
G1 X137.583 Y127.349
G1 X129.349 Y135.583 E.35779
G1 X128.816 Y135.583
G1 X137.583 Y126.816 E.38096
G1 X137.583 Y126.283
G1 X128.283 Y135.583 E.40413
G1 X127.749 Y135.583
G1 X137.583 Y125.749 E.42731
G1 X137.583 Y125.216
G1 X127.216 Y135.583 E.45048
G1 X126.683 Y135.583
G1 X137.583 Y124.683 E.47365
G1 X137.583 Y124.15
G1 X126.15 Y135.583 E.49682
G1 X125.616 Y135.583
G1 X137.583 Y123.616 E.52
G1 X137.583 Y123.083
G1 X125.083 Y135.583 E.54317
G1 X124.55 Y135.583
G1 X137.583 Y122.55 E.56634
G1 X137.583 Y122.017
G1 X124.016 Y135.583 E.58951
G1 X123.483 Y135.583
G1 X137.583 Y121.483 E.61269
G1 X137.583 Y120.95
G1 X122.95 Y135.583 E.63586
G1 X122.417 Y135.583
G1 X137.583 Y120.417 E.65903
G1 X137.583 Y119.883
G1 X121.883 Y135.583 E.6822
G1 X121.35 Y135.583
G1 X137.583 Y119.35 E.70538
G1 X137.583 Y118.817
G1 X120.817 Y135.583 E.72855
G1 X120.284 Y135.583
G1 X137.583 Y118.284 E.75172
G1 X137.583 Y117.75
G1 X119.75 Y135.583 E.77489
G1 X119.217 Y135.583
G1 X137.583 Y117.217 E.79807
G1 X137.583 Y116.684
G1 X118.684 Y135.583 E.82124
M73 P97 R0
G1 X118.417 Y135.316
G1 X137.316 Y116.417 E.82123
G1 X136.783 Y116.417
G1 X118.417 Y134.783 E.79806
G1 X118.417 Y134.249
G1 X136.249 Y116.417 E.77489
G1 X135.716 Y116.417
G1 X118.417 Y133.716 E.75172
G1 X118.417 Y133.183
G1 X135.183 Y116.417 E.72854
G1 X134.65 Y116.417
G1 X118.417 Y132.65 E.70537
G1 X118.417 Y132.116
G1 X134.116 Y116.417 E.6822
G1 X133.583 Y116.417
G1 X118.417 Y131.583 E.65902
G1 X118.417 Y131.05
G1 X133.05 Y116.417 E.63585
G1 X132.517 Y116.417
G1 X118.417 Y130.517 E.61268
G1 X118.417 Y129.983
G1 X131.983 Y116.417 E.58951
G1 X131.45 Y116.417
G1 X118.417 Y129.45 E.56633
G1 X118.417 Y128.917
G1 X130.917 Y116.417 E.54316
G1 X130.384 Y116.417
G1 X118.417 Y128.384 E.51999
G1 X118.417 Y127.85
G1 X129.85 Y116.417 E.49682
G1 X129.317 Y116.417
G1 X118.417 Y127.317 E.47364
G1 X118.417 Y126.784
G1 X128.784 Y116.417 E.45047
G1 X128.251 Y116.417
G1 X118.417 Y126.251 E.4273
G1 X118.417 Y125.717
G1 X127.717 Y116.417 E.40413
G1 X127.184 Y116.417
G1 X118.417 Y125.184 E.38095
G1 X118.417 Y124.651
G1 X126.651 Y116.417 E.35778
G1 X126.118 Y116.417
G1 X118.417 Y124.118 E.33461
G1 X118.417 Y123.584
G1 X125.584 Y116.417 E.31144
G1 X125.051 Y116.417
G1 X118.417 Y123.051 E.28826
G1 X118.417 Y122.518
G1 X124.518 Y116.417 E.26509
G1 X123.984 Y116.417
G1 X118.417 Y121.984 E.24192
G1 X118.417 Y121.451
G1 X123.451 Y116.417 E.21875
G1 X122.918 Y116.417
G1 X118.417 Y120.918 E.19557
G1 X118.417 Y120.385
G1 X122.385 Y116.417 E.1724
G1 X121.851 Y116.417
G1 X118.417 Y119.851 E.14923
G1 X118.417 Y119.318
G1 X121.318 Y116.417 E.12605
G1 X120.785 Y116.417
G1 X118.417 Y118.785 E.10288
G1 X118.417 Y118.252
G1 X120.252 Y116.417 E.07971
G1 X119.718 Y116.417
G1 X118.417 Y117.718 E.05654
G1 X118.417 Y117.185
G1 X119.185 Y116.417 E.03336
; close powerlost recovery
M1003 S0
; WIPE_START
G1 F12000
M204 S10000
G1 X118.417 Y117.185 E-.41261
G1 X118.417 Y117.718 E-.20264
G1 X118.687 Y117.449 E-.14475
; WIPE_END
G1 E-.04 F1800
G17
G3 Z20.4 I1.217 J0 P1  F30000
M106 S0
M106 P2 S0
M981 S0 P20000 ; close spaghetti detector
; FEATURE: Custom
; MACHINE_END_GCODE_START
; filament end gcode 

;===== date: 20240528 =====================
M400 ; wait for buffer to clear
G92 E0 ; zero the extruder
G1 E-0.8 F1800 ; retract
G1 Z20.5 F900 ; lower z a little
G1 X65 Y245 F12000 ; move to safe pos
G1 Y265 F3000

G1 X65 Y245 F12000
G1 Y265 F3000
M140 S0 ; turn off bed
M106 S0 ; turn off fan
M106 P2 S0 ; turn off remote part cooling fan
M106 P3 S0 ; turn off chamber cooling fan

G1 X100 F12000 ; wipe
; pull back filament to AMS
M620 S255
G1 X20 Y50 F12000
G1 Y-3
T255
G1 X65 F12000
G1 Y265
G1 X100 F12000 ; wipe
M621 S255
M104 S0 ; turn off hotend

M622.1 S1 ; for prev firware, default turned on
M1002 judge_flag timelapse_record_flag
M622 J1
    M400 ; wait all motion done
    M991 S0 P-1 ;end smooth timelapse at safe pos
    M400 S3 ;wait for last picture to be taken
M623; end of "timelapse_record_flag"

M400 ; wait all motion done
M17 S
M17 Z0.4 ; lower z motor current to reduce impact if there is something in the bottom

    G1 Z120 F600
    G1 Z118

M400 P100
M17 R ; restore z current

M220 S100  ; Reset feedrate magnitude
M201.2 K1.0 ; Reset acc magnitude
M73.2   R1.0 ;Reset left time magnitude
M1002 set_gcode_claim_speed_level : 0
;=====printer finish  sound=========
M17
M400 S1
M1006 S1
M1006 A0 B20 L100 C37 D20 M40 E42 F20 N60
M1006 A0 B10 L100 C44 D10 M60 E44 F10 N60
M1006 A0 B10 L100 C46 D10 M80 E46 F10 N80
M1006 A44 B20 L100 C39 D20 M60 E48 F20 N60
M1006 A0 B10 L100 C44 D10 M60 E44 F10 N60
M1006 A0 B10 L100 C0 D10 M60 E0 F10 N60
M1006 A0 B10 L100 C39 D10 M60 E39 F10 N60
M1006 A0 B10 L100 C0 D10 M60 E0 F10 N60
M1006 A0 B10 L100 C44 D10 M60 E44 F10 N60
M1006 A0 B10 L100 C0 D10 M60 E0 F10 N60
M1006 A0 B10 L100 C39 D10 M60 E39 F10 N60
M1006 A0 B10 L100 C0 D10 M60 E0 F10 N60
M1006 A0 B10 L100 C48 D10 M60 E44 F10 N100
M1006 A0 B10 L100 C0 D10 M60 E0 F10  N100
M1006 A49 B20 L100 C44 D20 M100 E41 F20 N100
M1006 A0 B20 L100 C0 D20 M60 E0 F20 N100
M1006 A0 B20 L100 C37 D20 M30 E37 F20 N60
M1006 W

M17 X0.8 Y0.8 Z0.5 ; lower motor current to 45% power
M960 S5 P0 ; turn off logo lamp
M73 P100 R0
; EXECUTABLE_BLOCK_END

